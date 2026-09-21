using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Workout;
using NSubstitute;

namespace MuscleCuties.Core.Tests.ViewModels.Workout;

public class WorkoutViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly IWorkoutService _workoutService = Substitute.For<IWorkoutService>();
    private readonly IWorkoutInjuryRepository _injuryRepo = Substitute.For<IWorkoutInjuryRepository>();

    private IServiceScopeFactory BuildScopeFactory()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthService)).Returns(_authService);
        serviceProvider.GetService(typeof(ICycleService)).Returns(_cycleService);
        serviceProvider.GetService(typeof(IWorkoutService)).Returns(_workoutService);
        serviceProvider.GetService(typeof(IWorkoutInjuryRepository)).Returns(_injuryRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return scopeFactory;
    }

    private WorkoutViewModel CreateViewModel()
    {
        _injuryRepo.GetActiveAsync(Arg.Any<int>())
            .Returns(new List<Models.Entities.Workout.Planning.WorkoutInjuryLog>());
        return new WorkoutViewModel(BuildScopeFactory());
    }

    [Fact]
    public async Task LoadData_AwaitsPhaseBeforeQueryingInjuriesInTheSameScope()
    {
        var vm = CreateViewModel();
        var phaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var phaseResult = new TaskCompletionSource<CyclePhase>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlapped = false;
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(_ =>
        {
            phaseStarted.TrySetResult();
            return phaseResult.Task;
        });
        _injuryRepo.GetActiveAsync(1).Returns(_ =>
        {
            overlapped = !phaseResult.Task.IsCompleted;
            return Array.Empty<Models.Entities.Workout.Planning.WorkoutInjuryLog>();
        });
        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Follicular)
            .Returns(new WorkoutPlanSummary(null, [], []));

        var load = vm.LoadDataCommand.ExecuteAsync(null);
        try
        {
            await phaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            phaseResult.TrySetResult(CyclePhase.Follicular);
            await load.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.False(overlapped);
        Assert.False(vm.IsLoadError);
        await _injuryRepo.Received(1).GetActiveAsync(1);
    }

    [Fact]
    public async Task LoadData_FailureAndRepeatedRetry_EndsInEmptyStateAfterRecovery()
    {
        var vm = CreateViewModel();
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Follicular)
            .Returns(Task.FromException<WorkoutPlanSummary>(new InvalidOperationException("query failed")));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await vm.LoadDataCommand.ExecuteAsync(null);
            Assert.True(vm.IsLoadError);
            Assert.False(vm.IsBusy);
            Assert.False(vm.IsPageLoading);
            Assert.False(vm.HasNoWorkouts);
        }

        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Follicular)
            .Returns(new WorkoutPlanSummary(null, [], []));
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.False(vm.IsLoadError);
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsPageLoading);
        Assert.True(vm.HasNoWorkouts);
        await _workoutService.Received(3).GetPlanSummaryAsync(1, CyclePhase.Follicular);
    }

    [Fact]
    public async Task InjuryModal_OpensLoadsAndCloses()
    {
        var vm = CreateViewModel();
        _authService.GetCurrentUserIdAsync().Returns(1);
        _injuryRepo.GetAllAsync(1).Returns(Array.Empty<Models.Entities.Workout.Planning.WorkoutInjuryLog>());

        await vm.OpenInjuryModalCommand.ExecuteAsync(null);

        Assert.True(vm.IsInjuryModalVisible);
        Assert.True(vm.InjuryLogVm.HasNoInjuries);
        vm.CloseInjuryModalCommand.Execute(null);
        Assert.False(vm.IsInjuryModalVisible);
    }

    [Fact]
    public async Task LoadData_WithActivePlan_LoadsWorkoutDays()
    {
        var plan = new WorkoutPlan
        { Id = 10, UserId = 1, Name = "Full Body", CyclePhaseTarget = CyclePhase.Follicular };
        var days = new List<WorkoutDay>
        {
            new() { Id = 1, WorkoutPlanId = 10, DayOfWeek = 1, Name = "Day 1" },
            new() { Id = 2, WorkoutPlanId = 10, DayOfWeek = 2, Name = "Day 2" }
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Follicular)
            .Returns(new WorkoutPlanSummary(
                plan,
                days,
                new WorkoutPlanner().BuildWorkoutItems(days)));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(plan, vm.ActivePlan);
        Assert.Equal(2, vm.WorkoutDays.Count);
        Assert.True(vm.HasWorkouts);
        Assert.False(vm.HasNoWorkouts);
        Assert.Equal("Full Body", vm.ActivePlanTitle);
        Assert.Equal("2 training days - 5 full rest days", vm.PlanSummaryText);
        Assert.Equal(2, vm.Workouts.Count);
    }

    [Fact]
    public async Task LoadData_WithNoActivePlan_ActivePlanIsNull()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Follicular)
            .Returns(new WorkoutPlanSummary(null, [], []));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Null(vm.ActivePlan);
        Assert.Empty(vm.WorkoutDays);
        Assert.True(vm.HasNoWorkouts);
        Assert.False(vm.HasWorkouts);
        Assert.Equal("No active workout plan", vm.EmptyWorkoutsTitle);
        Assert.Equal("Refresh", vm.EmptyWorkoutsButtonText);
    }

    [Fact]
    public async Task SelectFilter_WithNoMatchingWorkout_ShowsFilterEmptyStateAndCanShowAll()
    {
        var plan = new WorkoutPlan
        { Id = 10, UserId = 1, Name = "Full Body", CyclePhaseTarget = CyclePhase.Follicular };
        var days = new List<WorkoutDay>
        {
            new() { Id = 1, WorkoutPlanId = 10, DayOfWeek = 1, Name = "Strength day" }
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Follicular)
            .Returns(new WorkoutPlanSummary(
                plan,
                days,
                new WorkoutPlanner().BuildWorkoutItems(days)));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var cardioFilter = vm.Filters.Single(filter => filter.Label == "Cardio");
        vm.SelectFilterCommand.Execute(cardioFilter);

        Assert.Empty(vm.Workouts);
        Assert.True(vm.HasNoWorkouts);
        Assert.Equal("No cardio workouts", vm.EmptyWorkoutsTitle);
        Assert.Equal("Show All", vm.EmptyWorkoutsButtonText);

        await vm.EmptyWorkoutsActionCommand.ExecuteAsync(null);

        Assert.Single(vm.Workouts);
        Assert.True(vm.HasWorkouts);
    }

    [Fact]
    public async Task LoadData_WithTodayWorkout_BuildsFeaturedSessionCard()
    {
        var plan = new WorkoutPlan { Id = 10, UserId = 1, Name = "Full Body", CyclePhaseTarget = CyclePhase.Ovulatory };
        var todayWorkout = new WorkoutDay
        {
            Id = 1,
            WorkoutPlanId = 10,
            DayOfWeek = (int)DateTime.Today.DayOfWeek,
            WorkoutType = WorkoutType.Strength,
            Name = "Lower body strength"
        };
        todayWorkout.WorkoutDayExercises =
        [
            new WorkoutDayExercise { WorkoutDayId = 1, DurationSeconds = 600 },
            new WorkoutDayExercise { WorkoutDayId = 1, DurationSeconds = 600 },
            new WorkoutDayExercise { WorkoutDayId = 1, DurationSeconds = 600 }
        ];

        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Ovulatory);
        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Ovulatory)
            .Returns(new WorkoutPlanSummary(
                plan,
                [todayWorkout],
                new WorkoutPlanner().BuildWorkoutItems([todayWorkout])));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("Today strength", vm.FeaturedWorkoutBadgeText);
        Assert.Equal("Lower body strength", vm.FeaturedWorkoutTitle);
        Assert.Equal("30 min", vm.FeaturedWorkoutDurationText);
        Assert.Equal("3", vm.FeaturedWorkoutExercisesCount);
        Assert.Equal("Heavy", vm.FeaturedWorkoutIntensity);
    }

    [Fact]
    public async Task OpenWorkoutCommand_NavigatesToGuidedSession()
    {
        var routes = new List<string>();
        var vm = new WorkoutViewModel(BuildScopeFactory(), route =>
        {
            routes.Add(route);
            return Task.CompletedTask;
        });

        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem { WorkoutDayId = 42 });

        Assert.Equal(["WorkoutSessionPage?workoutDayId=42"], routes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task OpenWorkoutCommand_InvalidDay_DoesNotNavigate(int dayId)
    {
        var routes = new List<string>();
        var vm = new WorkoutViewModel(BuildScopeFactory(), route =>
        {
            routes.Add(route);
            return Task.CompletedTask;
        });

        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem { WorkoutDayId = dayId });
        await vm.OpenWorkoutCommand.ExecuteAsync(null);

        Assert.Empty(routes);
    }
}
