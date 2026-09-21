using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Models.Workout.Logging;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.ViewModels.Workout;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MuscleCuties.Core.Tests.ViewModels.Workout;

public class WorkoutSessionViewModelTests
{
    private readonly IWorkoutService _workouts = Substitute.For<IWorkoutService>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly ICycleService _cycle = Substitute.For<ICycleService>();
    private readonly List<string> _routes = [];
    private readonly WorkoutSessionViewModel _vm;
    private readonly IServiceScopeFactory _scopes;
    private readonly WorkoutExerciseItem[] _exercises = [Exercise(11), Exercise(12)];

    public WorkoutSessionViewModelTests()
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IWorkoutService)).Returns(_workouts);
        services.GetService(typeof(IAuthService)).Returns(_auth);
        services.GetService(typeof(ICycleService)).Returns(_cycle);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(services);
        _scopes = Substitute.For<IServiceScopeFactory>();
        _scopes.CreateScope().Returns(scope);
        _auth.GetCurrentUserIdAsync().Returns(7);
        _cycle.GetCurrentPhaseAsync(7).Returns(CyclePhase.Luteal);
        _workouts.GetWorkoutSessionDetailAsync(7, 42).Returns(Detail(_exercises));
        _vm = new WorkoutSessionViewModel(_scopes, route => { _routes.Add(route); return Task.CompletedTask; });
    }

    private static WorkoutExerciseItem Exercise(int id) => new()
    {
        WorkoutDayExerciseId = id, ExerciseId = id + 100, Name = $"Exercise {id}",
        ActivityTag = "STRENGTH", ActivityTitle = "Upper body", TargetText = "3 sets x 10 reps",
        RecommendationText = "RPE 7-8", PreviousText = "Last 10 kg with 3 x 10",
        LoggedSetsText = "3", LoggedRepsText = "10", LoggedWeightText = "10"
    };

    private static WorkoutSessionDetail Detail(params WorkoutExerciseItem[] exercises) => new(
        42, "Monday — Upper body", "STRENGTH", "2 exercises", exercises)
    {
        Activities = [new WorkoutActivitySectionItem
        {
            Title = "Upper body", Tag = "STRENGTH", Exercises = new ObservableCollection<WorkoutExerciseItem>(exercises)
        }]
    };

    private async Task Start()
    {
        await _vm.LoadSession(42);
        _vm.SelectActivityCommand.Execute(_vm.ActivityCards[0]);
        _vm.StartActivityCommand.Execute(null);
    }

    [Fact]
    public async Task LoadSession_ValidDayId_ShowsDayOverview()
    {
        await _vm.LoadSession(42);
        Assert.Equal(SessionScreen.DayOverview, _vm.CurrentScreen);
        Assert.Equal("Monday — Upper body", _vm.DayTitle);
        Assert.Single(_vm.ActivityCards);
        Assert.Contains("Luteal phase", _vm.CyclePhaseNote);
        Assert.False(_vm.IsBusy);
        Assert.False(_vm.HasError);
    }

    [Fact]
    public async Task SelectActivity_TransitionsToIntro()
    {
        await _vm.LoadSession(42);
        var activity = _vm.ActivityCards[0];
        _vm.SelectActivityCommand.Execute(activity);
        Assert.Equal(SessionScreen.ActivityIntro, _vm.CurrentScreen);
        Assert.Same(activity, _vm.SelectedActivity);
        Assert.Equal(["1. Exercise 11", "2. Exercise 12"], _vm.ExercisePreviewNames);
        Assert.Equal("Target RPE 7-8", _vm.ActivityIntroIntensity);
    }

    [Fact]
    public async Task StartActivity_SetsFirstExercise()
    {
        await Start();
        Assert.Equal(SessionScreen.ExerciseFlow, _vm.CurrentScreen);
        Assert.Equal(0, _vm.CurrentExerciseIndex);
        Assert.Equal(2, _vm.TotalExercisesInActivity);
        Assert.Same(_exercises[0], _vm.CurrentExercise);
        Assert.True(_vm.ExerciseProgress[0].IsCurrent);
    }

    [Fact]
    public async Task NextExercise_AdvancesIndex()
    {
        await Start();
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.Equal(1, _vm.CurrentExerciseIndex);
        Assert.True(_exercises[0].IsLogged);
        await _workouts.Received(1).LogWorkoutSessionAsync(7, 42,
            Arg.Is<IReadOnlyCollection<WorkoutExerciseLogInput>>(logs => logs.Count == 1 &&
                logs.Single().WorkoutDayExerciseId == 11 && logs.Single().CompletedSets == 3 &&
                logs.Single().CompletedReps == 10 && logs.Single().WeightKg == 10), DateTime.Today);
    }

    [Fact]
    public async Task NextExercise_LastExercise_TransitionsToSummary()
    {
        await Start();
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.Equal(SessionScreen.ActivitySummary, _vm.CurrentScreen);
        Assert.Equal("600 kg", _vm.SummaryTotalVolume);
        Assert.Equal("2 of 2 logged", _vm.SummaryStatusText);
        Assert.Equal("Activity complete!", _vm.SummaryTitle);
    }

    [Fact]
    public async Task SkipExercise_AdvancesWithoutLogging()
    {
        await Start();
        _vm.SkipExerciseCommand.Execute(null);
        Assert.Equal(1, _vm.CurrentExerciseIndex);
        Assert.False(_exercises[0].IsLogged);
        Assert.True(_vm.ExerciseProgress[0].IsSkipped);
        await _workouts.DidNotReceiveWithAnyArgs().LogWorkoutSessionAsync(default, default, default!, default);
        _vm.SkipExerciseCommand.Execute(null);
        Assert.Equal(SessionScreen.ActivitySummary, _vm.CurrentScreen);
        Assert.Equal("Activity reviewed", _vm.SummaryTitle);
        Assert.Equal("0 of 2 logged · 2 skipped", _vm.SummaryStatusText);
    }

    [Fact]
    public async Task JumpToExercise_SetsCorrectIndex()
    {
        await Start();
        _vm.JumpToExerciseCommand.Execute(1);
        Assert.Equal(1, _vm.CurrentExerciseIndex);
        Assert.Same(_exercises[1], _vm.CurrentExercise);
        _vm.JumpToExerciseCommand.Execute(99);
        Assert.Equal(1, _vm.CurrentExerciseIndex);
        _vm.PreviousExerciseCommand.Execute(null);
        Assert.Equal(0, _vm.CurrentExerciseIndex);
    }

    [Fact]
    public async Task JumpToLastExercise_ThenLog_ReturnsToUnvisitedExercise()
    {
        await Start();
        _vm.JumpToExerciseCommand.Execute(1);
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.Equal(SessionScreen.ExerciseFlow, _vm.CurrentScreen);
        Assert.Equal(0, _vm.CurrentExerciseIndex);
    }

    [Fact]
    public async Task SwapExercise_ReloadsCurrentExercise()
    {
        await Start();
        _exercises[1].LoggedWeightText = "17.5";
        var option = new ExerciseSwapOption { ExerciseId = 300, Name = "Incline press" };
        _workouts.GetSwapCandidatesAsync(7, 11).Returns([option]);
        await _vm.OpenSwapPanelCommand.ExecuteAsync(null);
        _vm.SelectSwapCandidateCommand.Execute(option);
        var replacement = Exercise(11);
        replacement.ExerciseId = 300;
        replacement.Name = "Incline press";
        _workouts.GetWorkoutSessionDetailAsync(7, 42).Returns(Detail(replacement, Exercise(12)));
        await _vm.ConfirmSwapCommand.ExecuteAsync(null);
        await _workouts.Received(1).SwapExerciseAsync(7, 11, 300, true);
        Assert.Same(replacement, _vm.CurrentExercise);
        Assert.Equal(0, _vm.CurrentExerciseIndex);
        Assert.Equal("17.5", _vm.SelectedActivity!.Exercises[1].LoggedWeightText);
        Assert.Contains("1. Incline press", _vm.ExercisePreviewNames);
        Assert.False(_vm.IsSwapPanelVisible);
    }

    [Theory]
    [InlineData("Last 10 kg with 3 x 10", "12", CelebrationType.BigPR)]
    [InlineData("3×12 @ 60kg", "65", CelebrationType.BigPR)]
    [InlineData("Last Bodyweight with 3 x 10", "5", CelebrationType.BigPR)]
    [InlineData("No previous log", "12", CelebrationType.MediumFirstTime)]
    [InlineData("Last 10 kg with 3 x 10", "10", CelebrationType.SmallCheckmark)]
    public async Task CelebrationTier_WeightPR_ReturnsBigPR(string previous, string weight, CelebrationType expected)
    {
        await Start();
        _exercises[0].PreviousText = previous;
        _exercises[0].LoggedWeightText = weight;
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.Equal(expected, _vm.Celebration);
    }

    [Fact]
    public async Task FinishActivity_ReturnsToOverview()
    {
        await Start();
        _vm.SkipExerciseCommand.Execute(null);
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        _vm.FinishActivityCommand.Execute(null);
        Assert.Equal(SessionScreen.DayOverview, _vm.CurrentScreen);
        Assert.Null(_vm.SelectedActivity);
        Assert.Equal("1 of 2 logged · 1 skipped", _vm.ActivityCards[0].SummaryText);
    }

    [Fact]
    public async Task FailedLog_KeepsCurrentExerciseAndEntries()
    {
        await Start();
        _workouts.LogWorkoutSessionAsync(7, 42, Arg.Any<IReadOnlyCollection<WorkoutExerciseLogInput>>(), Arg.Any<DateTime>())
            .ThrowsAsync(new IOException("Offline"));
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.Equal(0, _vm.CurrentExerciseIndex);
        Assert.Equal("10", _vm.CurrentExercise!.LoggedWeightText);
        Assert.False(_vm.CurrentExercise.IsLogged);
        Assert.True(_vm.HasError);
        Assert.Equal(CelebrationType.None, _vm.Celebration);
        Assert.False(_vm.IsBusy);
    }

    [Theory]
    [InlineData("0", "10", "10")]
    [InlineData("3", "invalid", "10")]
    [InlineData("3", "10", "-1")]
    [InlineData("3", "10", "NaN")]
    [InlineData("3", "10", "Infinity")]
    [InlineData("3", "10", "")]
    public async Task InvalidMetrics_DoNotLogOrAdvance(string sets, string reps, string weight)
    {
        await Start();
        _exercises[0].LoggedSetsText = sets;
        _exercises[0].LoggedRepsText = reps;
        _exercises[0].LoggedWeightText = weight;
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.True(_vm.HasError);
        Assert.Equal(0, _vm.CurrentExerciseIndex);
        await _workouts.DidNotReceiveWithAnyArgs().LogWorkoutSessionAsync(default, default, default!, default);
    }

    [Fact]
    public async Task EnduranceMetrics_LogCorrectUnitsAndIgnoreStrengthFields()
    {
        _exercises[0].UsesEnduranceMetrics = true;
        _exercises[0].UsesDurationMetric = true;
        _exercises[0].UsesDistanceMetric = true;
        _exercises[0].UsesPaceMetric = true;
        _exercises[0].UsesHeartRateMetric = true;
        _exercises[0].UsesPowerMetric = true;
        _exercises[0].UsesCadenceMetric = true;
        _exercises[0].UsesEffortMetric = true;
        _exercises[0].LoggedDurationMinutesText = "30.5";
        _exercises[0].LoggedDistanceKmText = "5";
        _exercises[0].LoggedPaceText = "6:06";
        _exercises[0].LoggedHeartRateText = "135";
        _exercises[0].LoggedPowerWattsText = "100";
        _exercises[0].LoggedCadenceRpmText = "80";
        _exercises[0].LoggedEffortText = "6";
        await Start();
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        await _workouts.Received().LogWorkoutSessionAsync(7, 42,
            Arg.Is<IReadOnlyCollection<WorkoutExerciseLogInput>>(logs =>
                logs.Single() == new WorkoutExerciseLogInput(11, 111, 0, 0, null, 1830, 5, 135, 366, 100, 80, 6)), DateTime.Today);
    }

    [Fact]
    public async Task RepeatedLog_UpdatesVolumeWithoutDoubleCountingPR()
    {
        await Start();
        _exercises[0].LoggedWeightText = "20";
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        _vm.PreviousExerciseCommand.Execute(null);
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.Equal(CelebrationType.SmallCheckmark, _vm.Celebration);
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.Equal("900 kg", _vm.SummaryTotalVolume);
        Assert.Equal(1, _vm.SummaryPrCount);
    }

    [Fact]
    public async Task LoadFailure_CanRetry()
    {
        _workouts.GetWorkoutSessionDetailAsync(7, 42).ThrowsAsync(new IOException());
        await _vm.LoadSession(42);
        Assert.True(_vm.HasError);
        Assert.False(_vm.IsBusy);
        _workouts.GetWorkoutSessionDetailAsync(7, 42).Returns(Detail(_exercises));
        await _vm.RetryLoadCommand.ExecuteAsync(null);
        Assert.False(_vm.HasError);
        Assert.Single(_vm.ActivityCards);
    }

    [Fact]
    public async Task CycleFailure_DoesNotBlockWorkout()
    {
        _cycle.GetCurrentPhaseAsync(7).ThrowsAsync(new IOException());
        await _vm.LoadSession(42);
        Assert.False(_vm.HasError);
        Assert.Single(_vm.ActivityCards);
        Assert.Contains("unavailable", _vm.CyclePhaseNote);
    }

    [Fact]
    public async Task RestDay_ShowsEmptyOverviewAndCanExitWithoutLogging()
    {
        _workouts.GetWorkoutSessionDetailAsync(7, 42).Returns(new WorkoutSessionDetail(
            42, "Recovery day", "Rest", "No exercises", [], true));
        await _vm.LoadSession(42);
        Assert.True(_vm.HasNoActivities);
        Assert.True(_vm.IsDayOverview);
        await _vm.FinishSessionCommand.ExecuteAsync(null);
        Assert.Equal([".."], _routes);
        await _workouts.DidNotReceiveWithAnyArgs().LogWorkoutSessionAsync(default, default, default!, default);
    }

    [Fact]
    public async Task FailedSwap_KeepsPanelAndCurrentExercise()
    {
        await Start();
        var option = new ExerciseSwapOption { ExerciseId = 300 };
        _workouts.GetSwapCandidatesAsync(7, 11).Returns([option]);
        await _vm.OpenSwapPanelCommand.ExecuteAsync(null);
        _vm.SelectSwapCandidateCommand.Execute(option);
        _workouts.SwapExerciseAsync(7, 11, 300, true).ThrowsAsync(new IOException());
        await _vm.ConfirmSwapCommand.ExecuteAsync(null);
        Assert.True(_vm.IsSwapPanelVisible);
        Assert.True(_vm.HasSwapError);
        Assert.Same(_exercises[0], _vm.CurrentExercise);
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task ClosingSwapBeforeLoadCompletes_DiscardsCandidates()
    {
        await Start();
        var pending = new TaskCompletionSource<IReadOnlyList<ExerciseSwapOption>>();
        _workouts.GetSwapCandidatesAsync(7, 11).Returns(pending.Task);
        var loading = _vm.OpenSwapPanelCommand.ExecuteAsync(null);
        _vm.CloseSwapPanelCommand.Execute(null);
        pending.SetResult([new ExerciseSwapOption { ExerciseId = 300 }]);
        await loading;
        Assert.False(_vm.IsSwapPanelVisible);
        Assert.Empty(_vm.SwapCandidates);
    }

    [Fact]
    public async Task SwapSucceedsButReloadFails_BlocksStaleInputsAndRetriesOnlyReload()
    {
        await Start();
        var option = new ExerciseSwapOption { ExerciseId = 300 };
        _workouts.GetSwapCandidatesAsync(7, 11).Returns([option]);
        await _vm.OpenSwapPanelCommand.ExecuteAsync(null);
        _vm.SelectSwapCandidateCommand.Execute(option);
        _workouts.GetWorkoutSessionDetailAsync(7, 42).ThrowsAsync(new IOException());

        await _vm.ConfirmSwapCommand.ExecuteAsync(null);
        Assert.True(_vm.SwapNeedsReload);
        Assert.Equal("Reload exercise", _vm.SwapConfirmText);
        _vm.CloseSwapPanelCommand.Execute(null);
        _vm.FinishActivityCommand.Execute(null);
        await _vm.NextExerciseCommand.ExecuteAsync(null);
        Assert.True(_vm.IsSwapPanelVisible);
        Assert.Equal(SessionScreen.ExerciseFlow, _vm.CurrentScreen);
        await _workouts.DidNotReceiveWithAnyArgs().LogWorkoutSessionAsync(default, default, default!, default);

        var replacement = Exercise(11);
        replacement.ExerciseId = 300;
        _workouts.GetWorkoutSessionDetailAsync(7, 42).Returns(Detail(replacement, _exercises[1]));
        await _vm.ConfirmSwapCommand.ExecuteAsync(null);
        Assert.Same(replacement, _vm.CurrentExercise);
        Assert.False(_vm.SwapNeedsReload);
        Assert.False(_vm.IsSwapPanelVisible);
        await _workouts.Received(1).SwapExerciseAsync(7, 11, 300, true);
    }

    [Fact]
    public async Task LoadingAnotherDayDuringSave_DoesNotMutateNewSession()
    {
        await Start();
        var pending = new TaskCompletionSource();
        _workouts.LogWorkoutSessionAsync(7, 42, Arg.Any<IReadOnlyCollection<WorkoutExerciseLogInput>>(), Arg.Any<DateTime>())
            .Returns(pending.Task);
        var saving = _vm.NextExerciseCommand.ExecuteAsync(null);
        _workouts.GetWorkoutSessionDetailAsync(7, 43).Returns(Detail(Exercise(13)) with { WorkoutDayId = 43, Title = "Tuesday" });
        await _vm.LoadSession(43);
        pending.SetResult();
        await saving;
        Assert.Equal("Tuesday", _vm.DayTitle);
        Assert.True(_vm.IsDayOverview);
        Assert.False(_vm.HasError);
        Assert.False(_vm.ActivityCards[0].Exercises[0].IsLogged);
    }

    [Fact]
    public async Task NavigationDuringSave_DoesNotChangeExercise()
    {
        await Start();
        var pending = new TaskCompletionSource();
        _workouts.LogWorkoutSessionAsync(7, 42, Arg.Any<IReadOnlyCollection<WorkoutExerciseLogInput>>(), Arg.Any<DateTime>())
            .Returns(pending.Task);
        var saving = _vm.NextExerciseCommand.ExecuteAsync(null);
        _vm.SkipExerciseCommand.Execute(null);
        _vm.JumpToExerciseCommand.Execute(1);
        await _vm.BackCommand.ExecuteAsync(null);
        Assert.Equal(0, _vm.CurrentExerciseIndex);
        Assert.Equal(SessionScreen.ExerciseFlow, _vm.CurrentScreen);
        pending.SetResult();
        await saving;
        Assert.Equal(1, _vm.CurrentExerciseIndex);
    }

    [Fact]
    public async Task NewerLoad_WinsWhenOlderRequestFinishesLast()
    {
        var pending = new TaskCompletionSource<WorkoutSessionDetail>();
        _workouts.GetWorkoutSessionDetailAsync(7, 42).Returns(pending.Task);
        _workouts.GetWorkoutSessionDetailAsync(7, 43).Returns(Detail(Exercise(13)) with { WorkoutDayId = 43, Title = "Tuesday" });
        var oldLoad = _vm.LoadSession(42);
        await _vm.LoadSession(43);
        pending.SetResult(Detail(_exercises));
        await oldLoad;
        Assert.Equal("Tuesday", _vm.DayTitle);
        Assert.Single(_vm.ActivityCards[0].Exercises);
    }

    [Fact]
    public async Task FinishSession_NavigatesBack()
    {
        await _vm.LoadSession(42);
        await _vm.FinishSessionCommand.ExecuteAsync(null);
        Assert.Equal([".."], _routes);
    }

    [Fact]
    public async Task OpenWorkout_NavigatesToSessionPage()
    {
        var vm = new WorkoutViewModel(_scopes, route => { _routes.Add(route); return Task.CompletedTask; });
        await vm.OpenWorkoutCommand.ExecuteAsync(new WorkoutItem { WorkoutDayId = 42 });
        Assert.Equal(["WorkoutSessionPage?workoutDayId=42"], _routes);
    }
}
