using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Models.Workout.Logging;
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

    private void StubReloadAfterSave()
    {
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _workoutService.GetPlanSummaryAsync(1, CyclePhase.Follicular)
            .Returns(new WorkoutPlanSummary(null, [], []));
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
    public async Task OpenWorkoutCommand_LoadsWorkoutModal()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _workoutService.GetWorkoutSessionDetailAsync(1, 42)
            .Returns(new WorkoutSessionDetail(
                42,
                "Lower body strength",
                "Strength",
                "1 exercise",
                [
                    new WorkoutExerciseItem
                    {
                        WorkoutDayExerciseId = 7,
                        ExerciseId = 8,
                        Name = "Goblet Squat",
                        TargetText = "3 sets x 10 reps",
                        PreviousText = "No previous log",
                        RecommendationText = "Pick a steady starting weight.",
                        LoggedSetsText = "3",
                        LoggedRepsText = "10"
                    }
                ]));

        var vm = CreateViewModel();
        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem
        {
            WorkoutDayId = 42,
            Tag = "STRENGTH",
            Title = "Lower body strength",
            Duration = "24 min",
            ExerciseCountText = "1 exercise"
        });

        Assert.True(vm.IsWorkoutModalVisible);
        Assert.False(vm.IsWorkoutDetailLoading);
        Assert.Equal("Lower body strength", vm.SelectedWorkoutTitle);
        Assert.Single(vm.SelectedWorkoutExercises);
        Assert.True(vm.HasSelectedWorkoutExercises);
        Assert.True(vm.ShowWorkoutFooterAction);
        Assert.Equal("Submit workout", vm.WorkoutLogButtonText);
    }

    [Fact]
    public async Task SaveWorkoutSessionCommand_SendsExerciseWeightsToService()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        StubReloadAfterSave();
        _workoutService.GetWorkoutSessionDetailAsync(1, 42)
            .Returns(new WorkoutSessionDetail(
                42,
                "Lower body strength",
                "Strength",
                "1 exercise",
                [
                    new WorkoutExerciseItem
                    {
                        WorkoutDayExerciseId = 7,
                        ExerciseId = 8,
                        Name = "Goblet Squat",
                        TargetText = "3 sets x 10 reps",
                        PreviousText = "No previous log",
                        RecommendationText = "Pick a steady starting weight.",
                        LoggedSetsText = "3",
                        LoggedRepsText = "10",
                        LoggedWeightText = "25"
                    }
                ]));

        var vm = CreateViewModel();
        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem
        {
            WorkoutDayId = 42,
            Tag = "STRENGTH",
            Title = "Lower body strength",
            Duration = "24 min",
            ExerciseCountText = "1 exercise"
        });

        await vm.SaveWorkoutSessionCommand.ExecuteAsync(null);

        await _workoutService.Received(1).LogWorkoutSessionAsync(
            1,
            42,
            Arg.Is<IReadOnlyCollection<WorkoutExerciseLogInput>>(logs =>
                logs.Count == 1 &&
                logs.Single().WorkoutDayExerciseId == 7 &&
                logs.Single().ExerciseId == 8 &&
                logs.Single().CompletedSets == 3 &&
                logs.Single().CompletedReps == 10 &&
                logs.Single().WeightKg == 25f),
            DateTime.Today);
        Assert.False(vm.IsWorkoutModalVisible);
    }

    [Fact]
    public async Task SaveWorkoutSessionCommand_WithMissingStrengthWeight_DoesNotSave()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _workoutService.GetWorkoutSessionDetailAsync(1, 42)
            .Returns(new WorkoutSessionDetail(
                42,
                "Lower body strength",
                "Strength",
                "1 exercise",
                [
                    new WorkoutExerciseItem
                    {
                        WorkoutDayExerciseId = 7,
                        ExerciseId = 8,
                        Name = "Goblet Squat",
                        TargetText = "3 sets x 10 reps",
                        PreviousText = "No previous log",
                        RecommendationText = "Pick a steady starting weight.",
                        LoggedSetsText = "3",
                        LoggedRepsText = "10",
                        LoggedWeightText = string.Empty
                    }
                ]));

        var vm = CreateViewModel();
        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem
        {
            WorkoutDayId = 42,
            Tag = "STRENGTH",
            Title = "Lower body strength",
            Duration = "24 min",
            ExerciseCountText = "1 exercise"
        });

        await vm.SaveWorkoutSessionCommand.ExecuteAsync(null);

        Assert.True(vm.HasWorkoutModalError);
        Assert.Contains("Add kg", vm.WorkoutModalErrorText);
        await _workoutService.DidNotReceive().LogWorkoutSessionAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<IReadOnlyCollection<WorkoutExerciseLogInput>>(),
            Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SaveWorkoutSessionCommand_SendsCardioMetricsWithoutStrengthMetrics()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        StubReloadAfterSave();
        _workoutService.GetWorkoutSessionDetailAsync(1, 42)
            .Returns(new WorkoutSessionDetail(
                42,
                "Smooth Ride",
                "Cardio",
                "1 exercise with 30 min",
                [
                    new WorkoutExerciseItem
                    {
                        WorkoutDayExerciseId = 7,
                        ExerciseId = 8,
                        Name = "Zone 2 Ride",
                        UsesEnduranceMetrics = true,
                        TargetText = "30 min steady",
                        PreviousText = "No previous log",
                        RecommendationText = "Log pace and heart rate so your cardio trend gets smarter.",
                        LoggedDurationMinutesText = "30",
                        LoggedDistanceKmText = "6.2",
                        LoggedPaceText = "4:50",
                        LoggedHeartRateText = "142"
                    }
                ]));

        var vm = CreateViewModel();
        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem
        {
            WorkoutDayId = 42,
            Tag = "CARDIO",
            Title = "Smooth Ride",
            Duration = "30 min",
            ExerciseCountText = "1 exercise"
        });

        await vm.SaveWorkoutSessionCommand.ExecuteAsync(null);

        await _workoutService.Received(1).LogWorkoutSessionAsync(
            1,
            42,
            Arg.Is<IReadOnlyCollection<WorkoutExerciseLogInput>>(logs =>
                logs.Count == 1 &&
                logs.Single().WorkoutDayExerciseId == 7 &&
                logs.Single().CompletedSets == 0 &&
                logs.Single().CompletedReps == 0 &&
                logs.Single().WeightKg == null &&
                logs.Single().CompletedDurationSeconds == 1800 &&
                logs.Single().DistanceKm == 6.2f &&
                logs.Single().AverageHeartRateBpm == 142 &&
                logs.Single().PaceSecondsPerKm == 290),
            DateTime.Today);
    }

    [Fact]
    public async Task SaveWorkoutSessionCommand_RestDay_LogsWithoutExercises()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        StubReloadAfterSave();
        _workoutService.GetWorkoutSessionDetailAsync(1, 42)
            .Returns(new WorkoutSessionDetail(
                42,
                "Living happy life",
                "Pure rest day",
                "No exercises today.",
                [],
                true));

        var vm = CreateViewModel();
        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem
        {
            WorkoutDayId = 42,
            Tag = "REST",
            Title = "Living happy life",
            Duration = "Rest day",
            ExerciseCountText = "No exercises",
            IsRestDay = true
        });

        await vm.SaveWorkoutSessionCommand.ExecuteAsync(null);

        Assert.False(vm.IsWorkoutModalVisible);
        await _workoutService.Received(1).LogWorkoutSessionAsync(
            1,
            42,
            Arg.Is<IReadOnlyCollection<WorkoutExerciseLogInput>>(logs => logs.Count == 0),
            DateTime.Today);
    }
}
