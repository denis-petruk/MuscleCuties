using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Core.Tests.ViewModels;

public class WorkoutViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly IWorkoutRepository _workoutRepository = Substitute.For<IWorkoutRepository>();

    private WorkoutViewModel CreateViewModel() =>
        new(_authService, _cycleService, _workoutRepository);

    [Fact]
    public async Task LoadData_WithActivePlan_LoadsWorkoutDays()
    {
        var plan = new WorkoutPlan { Id = 10, UserId = 1, Name = "Full Body", CyclePhaseTarget = CyclePhase.Follicular };
        var days = new List<WorkoutDay>
        {
            new WorkoutDay { Id = 1, WorkoutPlanId = 10, DayOfWeek = 1, Name = "Day 1", WorkoutType = WorkoutType.Strength, DurationMinutes = 45 },
            new WorkoutDay { Id = 2, WorkoutPlanId = 10, DayOfWeek = 2, Name = "Day 2", WorkoutType = WorkoutType.Cardio, DurationMinutes = 30 }
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _workoutRepository.GetActivePlanAsync(1).Returns(plan);
        _workoutRepository.GetWorkoutDaysByPlanAsync(10).Returns(days);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(plan, vm.ActivePlan);
        Assert.Equal(2, vm.WorkoutDays.Count);
    }

    [Fact]
    public async Task LoadData_WithNoActivePlan_ActivePlanIsNull()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _workoutRepository.GetActivePlanAsync(1).Returns((WorkoutPlan?)null);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Null(vm.ActivePlan);
        Assert.Empty(vm.WorkoutDays);
    }

    [Fact]
    public async Task LoadData_WithActivePlan_WorkoutItemsHaveCorrectWorkoutType()
    {
        var plan = new WorkoutPlan { Id = 10, UserId = 1, Name = "Full Body", CyclePhaseTarget = CyclePhase.Follicular };
        var days = new List<WorkoutDay>
        {
            new WorkoutDay { Id = 1, WorkoutPlanId = 10, DayOfWeek = 1, Name = "Strength Day", WorkoutType = WorkoutType.Strength, DurationMinutes = 45,
                WorkoutDayExercises = new List<WorkoutDayExercise>
                {
                    new WorkoutDayExercise { Id = 1, ExerciseId = 1, Sets = 3, Reps = 12 },
                    new WorkoutDayExercise { Id = 2, ExerciseId = 2, Sets = 3, Reps = 10 }
                }},
            new WorkoutDay { Id = 2, WorkoutPlanId = 10, DayOfWeek = 3, Name = "Cardio Day", WorkoutType = WorkoutType.Cardio, DurationMinutes = 30,
                WorkoutDayExercises = new List<WorkoutDayExercise>() }
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _workoutRepository.GetActivePlanAsync(1).Returns(plan);
        _workoutRepository.GetWorkoutDaysByPlanAsync(10).Returns(days);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Workouts.Count);
        Assert.Equal(WorkoutType.Strength, vm.Workouts[0].WorkoutType);
        Assert.Equal(WorkoutType.Cardio,   vm.Workouts[1].WorkoutType);
        Assert.Equal(2, vm.Workouts[0].ExerciseCount);
        Assert.Equal("45 min", vm.Workouts[0].Duration);
    }

    [Fact]
    public async Task SelectFilter_Strength_OnlyStrengthWorkoutsVisible()
    {
        var plan = new WorkoutPlan { Id = 10, UserId = 1, Name = "Full Body" };
        var days = new List<WorkoutDay>
        {
            new WorkoutDay { Id = 1, WorkoutPlanId = 10, DayOfWeek = 1, Name = "Strength Day", WorkoutType = WorkoutType.Strength, DurationMinutes = 45, WorkoutDayExercises = new List<WorkoutDayExercise>() },
            new WorkoutDay { Id = 2, WorkoutPlanId = 10, DayOfWeek = 3, Name = "Cardio Day",   WorkoutType = WorkoutType.Cardio,   DurationMinutes = 30, WorkoutDayExercises = new List<WorkoutDayExercise>() }
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _workoutRepository.GetActivePlanAsync(1).Returns(plan);
        _workoutRepository.GetWorkoutDaysByPlanAsync(10).Returns(days);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var strengthFilter = vm.Filters.First(f => f.Label == "Strength");
        vm.SelectFilterCommand.Execute(strengthFilter);

        Assert.Single(vm.Workouts);
        Assert.Equal(WorkoutType.Strength, vm.Workouts[0].WorkoutType);
    }
}
