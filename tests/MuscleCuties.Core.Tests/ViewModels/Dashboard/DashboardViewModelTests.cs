using NSubstitute;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Quiz;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.Core.Tests.ViewModels.Dashboard;

public class DashboardViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly INutritionService _nutritionService = Substitute.For<INutritionService>();
    private readonly IWorkoutService _workoutService = Substitute.For<IWorkoutService>();
    private readonly IProgressSummaryService _progressSummaryService = Substitute.For<IProgressSummaryService>();
    private readonly IDashboardPlanner _dashboardPlanner = new DashboardPlanner();
    private readonly IHealthSyncService _healthSyncService = Substitute.For<IHealthSyncService>();

    private DashboardViewModel CreateViewModel() =>
        new(
            _authService,
            _userRepository,
            _cycleService,
            _nutritionService,
            _workoutService,
            _progressSummaryService,
            _dashboardPlanner,
            _healthSyncService,
            () => { },
            () => { },
            () => { });

    private void ConfigureDefaultUserData(CyclePhase phase = CyclePhase.Follicular)
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(1).Returns(new UserProfile
        {
            UserId = 1,
            Name = "Denis Petruk",
            Weight = 70f,
            WorkoutDaysPerWeek = 4,
            CycleLength = 28
        });
        _cycleService.GetPredictionAsync(1).Returns(new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = DateTime.Today.AddDays(-9),
            CurrentDay = 10,
            PredictedCycleLength = 28,
            CurrentPhase = phase,
            PredictedNextPeriodDate = DateTime.Today.AddDays(18),
            PredictedOvulationDate = DateTime.Today.AddDays(4),
            DaysUntilPeriod = 18,
            PredictionSource = "profile"
        });
        _nutritionService.GetConsumedTotalsAsync(1, Arg.Any<DateTime>())
            .Returns(new MacroNutrients(800f, 60f, 100f, 25f));
        _progressSummaryService.GetSummaryAsync(1, Arg.Any<DateTime>())
            .Returns(new ProgressSummary(3, 2, 4));
        _workoutService.GetTodaysSummaryAsync(1, phase, Arg.Any<DateTime>())
            .Returns(TodaysWorkoutSummary.RestDay);
        _healthSyncService.GetCachedWeeklySummaryAsync(1).Returns((HealthWeeklySummary?)null);
        _healthSyncService.GetStatusAsync(1).Returns(new HealthSyncStatus(null, false, true, null, "Not connected"));
        _healthSyncService.ShouldShowPromptAsync(1).Returns(false);
    }

    [Fact]
    public async Task LoadData_SetsCurrentPhase_AndCaloriesData()
    {
        ConfigureDefaultUserData();
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2000f, 150f, 200f, 70f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(CyclePhase.Follicular, vm.CurrentPhase);
        Assert.Equal(2000f, vm.TargetCalories);
        Assert.Equal(800f, vm.ConsumedCalories);
        Assert.Equal("Follicular", vm.PhaseLabel);
        Assert.Equal("phase_follicular_plant.json", vm.PhaseIllustrationSource);
        Assert.Contains("Denis", vm.Greetings);
        Assert.Equal("DAY 10 · FOLLICULAR PHASE", vm.PhaseBadgeText);
        Assert.Equal("THIS WEEK · FOLLICULAR", vm.DashboardPhaseHeaderText);
        Assert.Equal("FOLLICULAR · DAY 10 / 28", vm.PhaseStatusText);
        Assert.Equal("Build momentum", vm.PhaseCardTitle);
        Assert.Equal(1, vm.CurrentPhaseColumn);
        Assert.Equal("+3%", vm.LoadAdjustmentText);
        Assert.Equal("18d", vm.NextPeriodValue);
        Assert.Equal("2.5 L", vm.HydrationConsumed);
        Assert.Equal("8h", vm.SleepGoal);
        Assert.Equal("2 day session streak", vm.WorkoutStreakText);
        Assert.Equal("4 day log streak", vm.NutritionStreakText);
        Assert.False(vm.IsRefreshing);
    }

    [Fact]
    public async Task LoadData_CalculatesCaloriesProgress()
    {
        ConfigureDefaultUserData(CyclePhase.Luteal);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Luteal)
            .Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedTotalsAsync(1, Arg.Any<DateTime>())
            .Returns(new MacroNutrients(2500f, 0f, 0f, 0f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1f, vm.CaloriesProgress);
    }

    [Fact]
    public async Task RefreshData_ResetsRefreshingState()
    {
        ConfigureDefaultUserData(CyclePhase.Ovulatory);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Ovulatory)
            .Returns((1900f, 140f, 190f, 65f));

        var vm = CreateViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsRefreshing);
        Assert.Equal(CyclePhase.Ovulatory, vm.CurrentPhase);
    }

    [Fact]
    public async Task LoadData_WithWorkoutPlan_LinksWorkoutBlockToTodaysWorkout()
    {
        ConfigureDefaultUserData(CyclePhase.Ovulatory);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Ovulatory)
            .Returns((2000f, 150f, 200f, 70f));

        var plan = new WorkoutPlan { Id = 7, UserId = 1, Name = "Cycle Strength", IsActive = true };
        var todayWorkout = new WorkoutDay
        {
            Id = 12,
            WorkoutPlanId = 7,
            DayOfWeek = (int)DateTime.Today.DayOfWeek,
            Name = "Lower Body"
        };
        todayWorkout.WorkoutDayExercises =
        [
            new WorkoutDayExercise { WorkoutDayId = 12, Sets = 3, Reps = 10 },
            new WorkoutDayExercise { WorkoutDayId = 12, Sets = 3, Reps = 12 },
            new WorkoutDayExercise { WorkoutDayId = 12, Sets = 4, Reps = 8 },
            new WorkoutDayExercise { WorkoutDayId = 12, Sets = 4, Reps = 8 }
        ];

        var workoutLog = new WorkoutLog { UserId = 1, WorkoutDayId = 12, CompletionPercent = 100 };
        _workoutService.GetTodaysSummaryAsync(1, CyclePhase.Ovulatory, Arg.Any<DateTime>())
            .Returns(new WorkoutPlanner().BuildTodaysSummary(
                plan,
                [todayWorkout],
                [workoutLog],
                CyclePhase.Ovulatory,
                DateTime.Today));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("Lower Body", vm.WorkoutTitle);
        Assert.Equal("52 min", vm.WorkoutDurationText);
        Assert.Equal("4", vm.WorkoutExercisesCount);
        Assert.Equal("High", vm.WorkoutIntensity);
        Assert.Equal("Completed", vm.SessionProgressText);
        Assert.Equal("Workout completed", vm.WorkoutBadgeText);
        Assert.Equal("Edit workout", vm.WorkoutActionText);
    }
}
