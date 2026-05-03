using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Core.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly INutritionService _nutritionService = Substitute.For<INutritionService>();
    private readonly IWorkoutService _workoutService = Substitute.For<IWorkoutService>();

    private DashboardViewModel CreateViewModel() =>
        new(_authService, _cycleService, _nutritionService, _workoutService, () => { }, () => { }, () => { });

    [Fact]
    public async Task LoadData_SetsCurrentPhase_AndCaloriesData()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(800f);
        _workoutService.GetTodaysWorkoutAsync(1).Returns((WorkoutDay?)null);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(CyclePhase.Follicular, vm.CurrentPhase);
        Assert.Equal(2000f, vm.TargetCalories);
        Assert.Equal(800f, vm.ConsumedCalories);
        Assert.Equal("Follicular", vm.PhaseLabel);
    }

    [Fact]
    public async Task LoadData_CalculatesCaloriesProgress()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Luteal);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Luteal)
            .Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(2500f);
        _workoutService.GetTodaysWorkoutAsync(1).Returns((WorkoutDay?)null);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1f, vm.CaloriesProgress);
    }

    [Fact]
    public async Task LoadData_TodaysWorkoutExists_SetsWorkoutTitle()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular).Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(0f);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>()).Returns((0f, 0f, 0f));
        _workoutService.GetTodaysWorkoutAsync(1).Returns(
            new WorkoutDay
            {
                Id = 5, Name = "Follicular Strength Day 1", WorkoutType = WorkoutType.Strength, DurationMinutes = 45,
                WorkoutDayExercises = new List<WorkoutDayExercise>
                {
                    new WorkoutDayExercise { Id = 1 },
                    new WorkoutDayExercise { Id = 2 },
                    new WorkoutDayExercise { Id = 3 }
                }
            });

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("Follicular Strength Day 1", vm.WorkoutTitle);
        Assert.Equal("3 exercises · 45 min", vm.WorkoutSubtitle);
        Assert.Equal("45 min", vm.WorkoutDurationText);
        Assert.Equal("HIGH", vm.WorkoutIntensity);
        Assert.Equal("STRENGTH", vm.SessionProgressText);
    }

    [Fact]
    public async Task LoadData_CardioWorkout_IntensityBasedOnPhase()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Ovulatory);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Ovulatory).Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(0f);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>()).Returns((0f, 0f, 0f));
        _workoutService.GetTodaysWorkoutAsync(1).Returns(
            new WorkoutDay
            {
                Id = 6, Name = "Ovulatory Cardio Day 1", WorkoutType = WorkoutType.Cardio, DurationMinutes = 45,
                WorkoutDayExercises = new List<WorkoutDayExercise> { new WorkoutDayExercise { Id = 1 } }
            });

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("HIGH", vm.WorkoutIntensity);
        Assert.Equal("CARDIO", vm.SessionProgressText);
        Assert.Equal("1 rounds · 45 min", vm.WorkoutSubtitle);
    }

    [Fact]
    public async Task LoadData_NoTodaysWorkout_ShowsRestDay()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Menstrual);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Menstrual).Returns((1800f, 130f, 180f, 60f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(0f);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>()).Returns((0f, 0f, 0f));
        _workoutService.GetTodaysWorkoutAsync(1).Returns((WorkoutDay?)null);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("Rest Day", vm.WorkoutTitle);
        Assert.Equal("REST DAY", vm.SessionProgressText);
    }
}
