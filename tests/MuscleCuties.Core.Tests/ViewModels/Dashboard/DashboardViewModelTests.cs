using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Dashboard;
using NSubstitute;

namespace MuscleCuties.Core.Tests.ViewModels.Dashboard;

public class DashboardViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly IDashboardPlanner _dashboardPlanner = new DashboardPlanner();
    private readonly IReadinessRepository _readinessRepository = Substitute.For<IReadinessRepository>();
    private readonly INutritionService _nutritionService = Substitute.For<INutritionService>();
    private readonly IProgressSummaryService _progressSummaryService = Substitute.For<IProgressSummaryService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IWorkoutService _workoutService = Substitute.For<IWorkoutService>();

    private IServiceScopeFactory BuildScopeFactory()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthService)).Returns(_authService);
        serviceProvider.GetService(typeof(IUserRepository)).Returns(_userRepository);
        serviceProvider.GetService(typeof(ICycleService)).Returns(_cycleService);
        serviceProvider.GetService(typeof(INutritionService)).Returns(_nutritionService);
        serviceProvider.GetService(typeof(IWorkoutService)).Returns(_workoutService);
        serviceProvider.GetService(typeof(IProgressSummaryService)).Returns(_progressSummaryService);
        serviceProvider.GetService(typeof(IDashboardPlanner)).Returns(_dashboardPlanner);
        serviceProvider.GetService(typeof(IReadinessRepository)).Returns(_readinessRepository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return scopeFactory;
    }

    private DashboardViewModel CreateViewModel()
    {
        return new DashboardViewModel(
            BuildScopeFactory(),
            () => Task.CompletedTask,
            () => Task.CompletedTask,
            () => Task.CompletedTask,
            () => Task.CompletedTask);
    }

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
        Assert.Equal(CyclePhaseAssets.FollicularAnimation, vm.PhaseIllustrationSource);
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

    [Fact]
    public async Task Invalidate_CausesNextLoadDataCommand_ToRefreshData()
    {
        ConfigureDefaultUserData();
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2000f, 150f, 200f, 70f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(2000f, vm.TargetCalories);

        // Change the data the service returns for the next load
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2500f, 180f, 250f, 80f));

        // Without Invalidate, the load gate would skip the reload because the data is still fresh
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.Equal(2000f, vm.TargetCalories);

        // After Invalidate, the next load should fetch fresh data
        vm.Invalidate();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(2500f, vm.TargetCalories);
    }

    [Fact]
    public void Greetings_MorningHour_ReturnsGoodMorning()
    {
        var vm = CreateViewModel();
        var hour = DateTime.Now.Hour;
        var greeting = vm.Greetings;

        if (hour < 12)
            Assert.StartsWith("Good morning", greeting);
        else if (hour < 18)
            Assert.StartsWith("Good afternoon", greeting);
        else
            Assert.StartsWith("Good evening", greeting);
    }

    [Fact]
    public async Task Greetings_AfterLoad_IncludesFirstName()
    {
        ConfigureDefaultUserData();
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2000f, 150f, 200f, 70f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Contains("Denis", vm.Greetings);
        Assert.DoesNotContain("Petruk", vm.Greetings);
    }

    [Fact]
    public void Greetings_WithoutDisplayName_ReturnsGreetingOnly()
    {
        var vm = CreateViewModel();

        // Before any load, DisplayName is empty
        var greeting = vm.Greetings;

        Assert.DoesNotContain(",", greeting);
        Assert.True(
            greeting.StartsWith("Good morning") ||
            greeting.StartsWith("Good afternoon") ||
            greeting.StartsWith("Good evening"));
    }

    [Fact]
    public void PhaseStatusText_WithActiveCycle_ShowsDayAndLength()
    {
        var vm = CreateViewModel();

        // Simulate an active cycle by setting properties directly
        vm.CurrentPhase = CyclePhase.Ovulatory;
        vm.CurrentCycleDay = 14;
        vm.PredictedCycleLength = 30;

        Assert.Equal("OVULATORY · DAY 14 / 30", vm.PhaseStatusText);
    }

    [Fact]
    public void PhaseStatusText_WithZeroCycleDay_ShowsStartTracking()
    {
        var vm = CreateViewModel();

        vm.CurrentPhase = CyclePhase.Follicular;
        vm.CurrentCycleDay = 0;

        Assert.Equal("FOLLICULAR · START TRACKING", vm.PhaseStatusText);
    }

    [Fact]
    public void PhaseStatusText_AllPhases_FormatsCorrectly()
    {
        var vm = CreateViewModel();
        vm.CurrentCycleDay = 5;
        vm.PredictedCycleLength = 28;

        vm.CurrentPhase = CyclePhase.Menstrual;
        Assert.Equal("MENSTRUAL · DAY 5 / 28", vm.PhaseStatusText);

        vm.CurrentPhase = CyclePhase.Luteal;
        Assert.Equal("LUTEAL · DAY 5 / 28", vm.PhaseStatusText);
    }

    [Fact]
    public async Task IsPageLoading_TrueDuringInitialLoad_FalseAfter()
    {
        ConfigureDefaultUserData();
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2000f, 150f, 200f, 70f));

        var vm = CreateViewModel();

        // Before any load, IsBusy is false and HasLoaded is false, so IsPageLoading is false
        Assert.False(vm.IsPageLoading);

        var loadingStates = new List<bool>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DashboardViewModel.IsPageLoading))
                loadingStates.Add(vm.IsPageLoading);
        };

        await vm.LoadDataCommand.ExecuteAsync(null);

        // After the load completes, IsPageLoading should be false
        Assert.False(vm.IsPageLoading);

        // During the load, IsPageLoading should have been true then reverted to false
        Assert.Contains(true, loadingStates);
        Assert.Equal(false, loadingStates.Last());
    }

    [Fact]
    public async Task IsPageLoading_FalseOnSubsequentLoad_BecauseHasLoadedIsTrue()
    {
        ConfigureDefaultUserData();
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2000f, 150f, 200f, 70f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        // Force a reload via Invalidate so the gate allows a second load
        vm.Invalidate();

        var loadingStates = new List<bool>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DashboardViewModel.IsPageLoading))
                loadingStates.Add(vm.IsPageLoading);
        };

        await vm.LoadDataCommand.ExecuteAsync(null);

        // After Invalidate, HasLoaded is reset, so IsPageLoading transitions should occur again
        Assert.Contains(true, loadingStates);
        Assert.False(vm.IsPageLoading);
    }
}
