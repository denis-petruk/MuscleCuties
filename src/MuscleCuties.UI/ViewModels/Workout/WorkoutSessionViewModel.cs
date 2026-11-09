using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

public enum SessionScreen { ActivityIntro, ExerciseFlow, TimedActivity, ActivitySummary }

public enum CelebrationType { None, SmallCheckmark, MediumFirstTime, BigPR }
public enum HiitConfirmationChoice { Cancel, Confirm, Update }

public sealed record SessionExerciseProgress(int Index, string Name, bool IsLogged, bool IsCurrent, bool IsSkipped)
{
    public string AccessibilityText => UiTextCatalog.Format(
        "Workout_Session_ExerciseProgressAccessibilityFormat",
        Index + 1,
        Name,
        UiTextCatalog.Get(IsCurrent ? "Workout_Session_ExerciseStateCurrent"
            : IsLogged ? "Workout_Session_ExerciseStateLogged"
            : IsSkipped ? "Workout_Session_ExerciseStateSkipped"
            : "Workout_Session_ExerciseStatePending"));
}

public partial class WorkoutSessionViewModel : ObservableObject, IDisposable
{
    private const int TimingCheckpointIntervalSeconds = 15;
    private const int SingleConcurrentOperation = 1;
    private const int NoCompletionWaitMilliseconds = 0;
    private readonly Dictionary<WorkoutActivitySectionItem, Stopwatch> _activityTimers = [];
    private readonly ICycleOverviewUseCase _cycleOverview;
    private readonly Func<string, Task> _navigateAsync;
    private readonly HashSet<int> _prIds = [];
    private readonly Dictionary<int, WorkoutExerciseLogDto> _savedLogs = [];
    private readonly IWorkoutSessionActionUseCase _session;
    private readonly IStrengthPlanProposalClientV020? _planning;
    private readonly Func<SavedActivityAnswersDto, Task<HiitConfirmationChoice>>? _confirmHiitAsync;
    private readonly Func<Task>? _updateHiitAsync;
    private readonly HashSet<int> _skippedIds = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNoActivities), nameof(HasActivityChoices))]
    private ObservableCollection<WorkoutActivitySectionItem> _activityCards = [];

    [ObservableProperty] private string _activityIntroIntensity = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNoActivityPrescription))]
    private string _activityPrescriptionText = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasCelebration))]
    private CelebrationType _celebration;

    [ObservableProperty] private string _celebrationText = string.Empty;
    [ObservableProperty] private WorkoutExerciseItem? _currentExercise;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ExercisePositionText))]
    private int _currentExerciseIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivityIntro), nameof(IsExerciseFlow),
        nameof(IsActivitySummary), nameof(IsTimedActivityScreen), nameof(IsTimedActivityActive),
        nameof(IsTimedActivitySummary), nameof(ShowsTimedActivityDistance),
        nameof(CanPauseTimedActivity), nameof(CanResumeTimedActivity))]
    private SessionScreen _currentScreen;

    [ObservableProperty] private string _cyclePhaseNote = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDoseAdjustmentNote))]
    private string _doseAdjustmentNote = string.Empty;
    public bool HasDoseAdjustmentNote => !string.IsNullOrWhiteSpace(DoseAdjustmentNote);
    [ObservableProperty] private string _dayTitle = "Workout";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError), nameof(HasNoActivities),
        nameof(HasActivityChoices))]
    private string _errorText = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _exercisePreviewNames = [];
    [ObservableProperty] private ObservableCollection<SessionExerciseProgress> _exerciseProgress = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanInteract), nameof(CanAbandonPlannedSession), nameof(HasNoActivities),
        nameof(HasActivityChoices), nameof(CanPauseTimedActivity), nameof(CanResumeTimedActivity))]
    private bool _isBusy;
    [ObservableProperty] private bool _isActivityComplete;

    [ObservableProperty] private bool _isRestTimerRunning;
    [ObservableProperty] private bool _isRestTimerVisible;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowNoSwapCandidates))]
    private bool _isSwapLoading;

    [ObservableProperty] private bool _isSwapPanelVisible;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TipsButtonText))]
    private bool _isTipsExpanded;

    private int _loadVersion;
    private CancellationTokenSource? _restTimerCancellation;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RestTimerDisplay), nameof(RestTimerProgress))]
    private int _restTimerRemainingSeconds;

    [ObservableProperty] private int _restTimerTotalSeconds;
    private int _restTimerVersion;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartActivity), nameof(ShowStrengthSummaryMetrics),
        nameof(IsTimedActivitySummary), nameof(ShowsTimedActivityDistance))]
    private WorkoutActivitySectionItem? _selectedActivity;
    [ObservableProperty] private string _summaryDuration = "<1 min";
    [ObservableProperty] private string _summaryEffortText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummaryDistance))]
    private string _summaryDistanceText = string.Empty;
    [ObservableProperty] private int _summaryPrCount;
    [ObservableProperty] private string _summaryStatusText = string.Empty;
    [ObservableProperty] private string _summaryTitle = "Workout_Session_SummaryCompleteTitle";
    [ObservableProperty] private string _summaryTotalVolume = "0 kg";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSwapCandidates), nameof(ShowNoSwapCandidates))]
    private ObservableCollection<ExerciseSwapOption> _swapCandidates = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSwapError), nameof(ShowNoSwapCandidates))]
    private string _swapErrorText = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDismissSwap), nameof(SwapConfirmText))]
    private bool _swapNeedsReload;

    [ObservableProperty] private string _swapTargetName = string.Empty;
    private int _swapVersion;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ExercisePositionText))]
    private int _totalExercisesInActivity;

    [ObservableProperty] private bool _isMovementNoteOpen;
    [ObservableProperty] private bool _isMovementNoteBusy;
    [ObservableProperty] private string _movementNoteText = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasMovementNoteError))]
    private string _movementNoteErrorText = string.Empty;
    public bool HasMovementNoteError => !string.IsNullOrWhiteSpace(MovementNoteErrorText);
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasMovementNoteStatus))]
    private string _movementNoteStatusText = string.Empty;
    public bool HasMovementNoteStatus => !string.IsNullOrWhiteSpace(MovementNoteStatusText);

    private int _workoutDayId;
    private bool _isRestDay;
    private Guid? _plannedSessionId;
    private DateTime _plannedSessionStartedAtUtc;
    private DateTime? _pendingCompletionAtUtc;
    private int? _pendingCompletionSeconds;
    private int? _pendingCompletionRpe;
    private decimal? _pendingCompletionDistanceKm;
    private readonly Stopwatch _plannedSessionTimer = new();
    private readonly SemaphoreSlim _timingGate = new(SingleConcurrentOperation, SingleConcurrentOperation);
    private readonly SemaphoreSlim _completionGate = new(SingleConcurrentOperation, SingleConcurrentOperation);
    private CancellationTokenSource? _timingLoopCancellation;
    private Guid? _timingIntervalId;
    private Guid? _pendingResumeOperationId;
    private PendingTimingCheckpoint? _pendingTimingCheckpoint;
    private int _confirmedActiveSeconds;
    private bool _sessionCreated;
    private bool _appSuspended;
    private bool _hasLoadedSession;
    private sealed record PendingTimingCheckpoint(Guid OperationId, Guid IntervalId, int MeasuredSeconds, bool Pause);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimedActivityActive), nameof(CanPauseTimedActivity),
        nameof(CanResumeTimedActivity), nameof(TimedActivityStateKey))]
    private bool _isTimedActivityRunning;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanStartActivity))]
    private bool _isActivityUnsafe;

    // Timed running surface. One timing authority stays behind these values: confirmed server time
    // plus the existing local stopwatch, exactly like the strength flow.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPauseTimedActivity), nameof(CanResumeTimedActivity),
        nameof(TimedActivityStateKey))]
    private bool _isTimedActivityPaused;
    [ObservableProperty] private bool _isAbandonConfirmationVisible;
    [ObservableProperty] private string _timedActivityElapsedText = "00:00";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTimedActivityPhase))]
    private string _timedActivityPhaseKey = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTimedActivityCountdown))]
    private string _timedActivityRemainingText = string.Empty;
    [ObservableProperty] private double _timedActivityProgressValue;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimedActivityRpe), nameof(TimedActivityRpeText),
        nameof(HasTimedActivityRpe))]
    private double _timedActivityRpeValue;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTimedActivityDistance))]
    private string _timedActivityDistanceText = string.Empty;
    private CancellationTokenSource? _timedDisplayCancellation;
    private bool _abandonOutcomeUnknown;

    public WorkoutSessionViewModel(
        IWorkoutSessionActionUseCase session,
        ICycleOverviewUseCase cycleOverview,
        Func<string, Task> navigateAsync,
        IStrengthPlanProposalClientV020? planning = null,
        Func<SavedActivityAnswersDto, Task<HiitConfirmationChoice>>? confirmHiitAsync = null,
        Func<Task>? updateHiitAsync = null)
    {
        _session = session;
        _cycleOverview = cycleOverview;
        _navigateAsync = navigateAsync;
        _planning = planning;
        _confirmHiitAsync = confirmHiitAsync;
        _updateHiitAsync = updateHiitAsync;
    }

    public void Dispose()
    {
        _timingLoopCancellation?.Cancel();
        _timingLoopCancellation?.Dispose();
        _timingLoopCancellation = null;
        StopTimedActivityDisplay();
        _timingGate.Dispose();
        _completionGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public int WorkoutDayId => _workoutDayId;
    public bool IsActivityIntro => CurrentScreen == SessionScreen.ActivityIntro;
    public bool IsExerciseFlow => CurrentScreen == SessionScreen.ExerciseFlow;
    public bool IsActivitySummary => CurrentScreen == SessionScreen.ActivitySummary;
    public bool IsTimedActivityScreen => CurrentScreen == SessionScreen.TimedActivity;
    public bool IsTimedActivityActive => IsTimedActivityScreen && _plannedSessionId.HasValue;
    public bool CanPauseTimedActivity =>
        IsTimedActivityActive && !IsTimedActivityPaused && !IsBusy && _pendingCompletionAtUtc is null;
    public bool CanResumeTimedActivity =>
        IsTimedActivityActive && IsTimedActivityPaused && !IsBusy && _pendingCompletionAtUtc is null;
    public bool HasTimedActivityPhase => !string.IsNullOrEmpty(TimedActivityPhaseKey);
    public bool HasTimedActivityCountdown => !string.IsNullOrEmpty(TimedActivityRemainingText);
    public bool ShowsTimedActivityDistance => IsTimedActivityScreen && SelectedActivity is not null &&
        TimedActivityProgress.SupportsDistance(SelectedActivity.Tag);
    public bool HasTimedActivityDistance => !string.IsNullOrWhiteSpace(TimedActivityDistanceText);
    public bool HasSummaryDistance => !string.IsNullOrEmpty(SummaryDistanceText);
    public bool IsTimedActivitySummary => IsActivitySummary && SelectedActivity?.IsTimedActivity == true;
    public bool IsRestDaySession => _isRestDay;
    public bool ShowStartButton => !_isRestDay;
    public bool HasTimedActivityRpe => TimedActivityRpe.HasValue;
    public int? TimedActivityRpe => TimedActivityProgress.NormalizeRpe(TimedActivityRpeValue);
    public string TimedActivityRpeText => TimedActivityRpe is { } rpe
        ? UiTextCatalog.Format("Workout_Session_TimedRpeValueFormat", rpe)
        : UiTextCatalog.Get("Workout_Session_TimedRpeUnrated");
    public string TimedActivityStateKey => IsTimedActivityPaused
        ? "Workout_Session_TimedStatePaused"
        : "Workout_Session_TimedStateRunning";
    public bool ShowStrengthSummaryMetrics => SelectedActivity?.IsTimedActivity != true;
    public bool CanAbandonPlannedSession => _sessionCreated && _plannedSessionId.HasValue && !IsBusy && _pendingCompletionAtUtc is null;
    public bool CanStartActivity => !IsActivityUnsafe && !_isRestDay && SelectedActivity is not null;
    public bool HasNoActivityPrescription => string.IsNullOrEmpty(ActivityPrescriptionText);
    public bool CanInteract => !IsBusy;

    public async Task SaveOptionalExerciseAsync(WorkoutExerciseItem item)
    {
        if (item.WorkoutDayExerciseId <= 0) return;
        if (!item.IsOptional) item.OptionalSets = 0;
        try
        {
            await _session.SetExerciseOptionalAsync(new SetWorkoutExerciseOptionalRequest(
                item.WorkoutDayExerciseId, item.IsOptional, item.IsOptional ? item.OptionalSets : 0));
        }
        catch (Exception ex)
        {
            ApplyFailure(ApiFailurePresenter.FromException(ex, "Ui_Workout_Error_SaveExercise"));
            try
            {
                var detail = await _session.GetDetailAsync(_workoutDayId);
                var saved = detail.Exercises.FirstOrDefault(e => e.WorkoutDayExerciseId == item.WorkoutDayExerciseId);
                if (saved is not null)
                {
                    item.IsOptional = saved.IsOptional;
                    item.OptionalSets = saved.OptionalSets;
                }
            }
            catch { }
            throw;
        }
    }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorReference))]
    private string _errorReference = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public bool HasErrorReference => HasError && !string.IsNullOrEmpty(ErrorReference);
    // The page is pushed before the session is fetched. Without this gate the empty state
    // renders for one frame and then disappears once the activities arrive.
    public bool HasNoActivities => _hasLoadedSession && !IsBusy && !HasError && ActivityCards.Count == 0;
    public bool HasActivityChoices => _hasLoadedSession && !IsBusy && !HasError && ActivityCards.Count > 0;
    public bool HasSwapCandidates => SwapCandidates.Count > 0;
    public bool HasSwapError => !string.IsNullOrEmpty(SwapErrorText);
    public bool CanDismissSwap => !SwapNeedsReload;
    public string SwapConfirmText => SwapNeedsReload
        ? UiTextCatalog.Get("Workout_Swap_ReloadExercise")
        : UiTextCatalog.Get("Workout_Swap_ConfirmSwap");
    public bool ShowNoSwapCandidates => !IsSwapLoading && !HasSwapCandidates && !HasSwapError;
    public bool HasCelebration => Celebration != CelebrationType.None;
    public string ExercisePositionText => UiTextCatalog.Format(
        "Workout_Session_ExercisePositionFormat", CurrentExerciseIndex + 1, TotalExercisesInActivity);
    public string TipsButtonText => UiTextCatalog.Get(
        IsTipsExpanded ? "Workout_Session_TipsHide" : "Workout_Session_TipsShow");
    public string RestTimerDisplay => $"{RestTimerRemainingSeconds / 60:00}:{RestTimerRemainingSeconds % 60:00}";

    public double RestTimerProgress => RestTimerTotalSeconds > 0
        ? Math.Clamp((double)RestTimerRemainingSeconds / RestTimerTotalSeconds, 0, 1)
        : 0;

    [RelayCommand]
    public void StartRestTimer(int seconds)
    {
        StopRestTimer();
        if (seconds <= 0) return;
        RestTimerTotalSeconds = Math.Min(seconds, 3600);
        RestTimerRemainingSeconds = RestTimerTotalSeconds;
        IsRestTimerVisible = true;
        IsRestTimerRunning = true;
        var cancellation = new CancellationTokenSource();
        _restTimerCancellation = cancellation;
        var version = ++_restTimerVersion;
        var context = SynchronizationContext.Current;
        _ = CountRestTimerAsync(version, context, cancellation.Token);
    }

    [RelayCommand]
    public void PauseRestTimer()
    {
        IsRestTimerRunning = false;
    }

    [RelayCommand]
    public void ResumeRestTimer()
    {
        if (IsRestTimerVisible && RestTimerRemainingSeconds > 0)
            IsRestTimerRunning = true;
    }

    [RelayCommand]
    public void AddRestTimerSeconds(int seconds)
    {
        if (!IsRestTimerVisible || seconds <= 0) return;
        var added = Math.Min(seconds, 3600);
        RestTimerRemainingSeconds = Math.Min(3600, RestTimerRemainingSeconds + added);
        RestTimerTotalSeconds = Math.Min(3600, RestTimerTotalSeconds + added);
        OnPropertyChanged(nameof(RestTimerProgress));
    }

    [RelayCommand]
    public void StopRestTimer()
    {
        ++_restTimerVersion;
        _restTimerCancellation?.Cancel();
        _restTimerCancellation?.Dispose();
        _restTimerCancellation = null;
        IsRestTimerRunning = false;
        IsRestTimerVisible = false;
        RestTimerRemainingSeconds = 0;
        RestTimerTotalSeconds = 0;
    }

    private async Task CountRestTimerAsync(int version, SynchronizationContext? context,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
                if (context is null)
                    AdvanceRestTimer(version);
                else
                    context.Post(_ => AdvanceRestTimer(version), null);
        }
        catch (OperationCanceledException) { }
    }

    private void AdvanceRestTimer(int version)
    {
        if (version != _restTimerVersion || !IsRestTimerRunning) return;
        RestTimerRemainingSeconds = Math.Max(0, RestTimerRemainingSeconds - 1);
        if (RestTimerRemainingSeconds == 0)
        {
            IsRestTimerRunning = false;
            _restTimerCancellation?.Cancel();
        }
    }

    // Awaitable for callers and tests; the query setter can start it without async void.
    public async Task LoadSession(int workoutDayId, string? activityTag = null)
    {
        if (_plannedSessionId.HasValue && !await SuspendTimingAsync())
            return;
        StopRestTimer();
        var version = ++_loadVersion;
        _workoutDayId = workoutDayId;
        _hasLoadedSession = false;
        OnPropertyChanged(nameof(HasNoActivities));
        OnPropertyChanged(nameof(HasActivityChoices));
        ResetSwapPanel();
        ActivityCards = [];
        SelectedActivity = null;
        CurrentExercise = null;
        CurrentExerciseIndex = 0;
        TotalExercisesInActivity = 0;
        ExerciseProgress = [];
        ExercisePreviewNames = [];
        DayTitle = "Workout";
        CyclePhaseNote = string.Empty;
        _skippedIds.Clear();
        _prIds.Clear();
        _savedLogs.Clear();
        _activityTimers.Clear();
        Celebration = CelebrationType.None;
        IsActivityComplete = false;
        CurrentScreen = SessionScreen.ActivityIntro;
        ErrorText = string.Empty;
        IsBusy = true;
        try
        {
            if (workoutDayId <= 0)
                throw new InvalidOperationException("This workout could not be found. Go back and choose a workout.");

            var detail = await _session.GetDetailAsync(workoutDayId);
            var existingSession = await _session.GetPlannedSessionAsync(workoutDayId);
            _isRestDay = detail.IsRestDay;
            OnPropertyChanged(nameof(CanStartActivity));
            OnPropertyChanged(nameof(IsRestDaySession));
            OnPropertyChanged(nameof(ShowStartButton));
            _plannedSessionId = existingSession is { IsCompleted: false, IsCanceled: false } ? existingSession.SessionId : null;
            _sessionCreated = _plannedSessionId.HasValue;
            IsActivityUnsafe = false;
            _confirmedActiveSeconds = existingSession?.ConfirmedActiveSeconds ?? 0;
            _timingIntervalId = null;
            _pendingResumeOperationId = null;
            _pendingTimingCheckpoint = null;
            OnPropertyChanged(nameof(CanAbandonPlannedSession));
            _plannedSessionStartedAtUtc = existingSession?.StartedAtUtc ?? default;
            _plannedSessionTimer.Reset();
            _pendingCompletionAtUtc = null;
            _pendingCompletionSeconds = null;
            IsTimedActivityRunning = false;
            IsTimedActivityPaused = false;
            IsAbandonConfirmationVisible = false;
            _abandonOutcomeUnknown = false;
            StopTimedActivityDisplay();
            TimedActivityElapsedText = "00:00";
            TimedActivityPhaseKey = string.Empty;
            TimedActivityRemainingText = string.Empty;
            TimedActivityProgressValue = 0;
            TimedActivityRpeValue = 0;
            TimedActivityDistanceText = string.Empty;
            SummaryEffortText = string.Empty;
            SummaryDistanceText = string.Empty;
            _pendingCompletionRpe = null;
            _pendingCompletionDistanceKm = null;
            string phaseNote;
            try
            {
                var overview = await _cycleOverview.LoadAsync(DateTime.Today, DateTime.Today);
                phaseNote = BuildPhaseNote(overview?.Prediction.CurrentPhase ?? CyclePhase.Unknown);
            }
            catch
            {
                phaseNote = "Workout_Session_CycleUnavailable";
            }

            if (version != _loadVersion) return;
            DayTitle = detail.Title;
            DoseAdjustmentNote = detail.DoseAdjustmentReason switch
            {
                WorkoutDoseAdjustmentReason.DailyReadiness => UiTextCatalog.Get("Workout_Session_AdjustmentDaily"),
                WorkoutDoseAdjustmentReason.ProvisionalPhaseFeeling => UiTextCatalog.Get("Workout_Session_AdjustmentPhase"),
                WorkoutDoseAdjustmentReason.RepeatedPhasePattern => UiTextCatalog.Get("Workout_Session_AdjustmentPattern"),
                WorkoutDoseAdjustmentReason.SafetyReview => UiTextCatalog.Get("Workout_Session_AdjustmentSafety"),
                _ => string.Empty
            };
            ActivityCards = new ObservableCollection<WorkoutActivitySectionItem>(GetActivities(detail));
            foreach (var item in ActivityCards.SelectMany(activity => activity.Exercises).Where(item => item.IsLogged))
                _savedLogs[item.WorkoutDayExerciseId] = BuildLogInput(item);
            RefreshActivityCards();
            CyclePhaseNote = phaseNote;
            IsBusy = false;
            var requestedActivity = !string.IsNullOrWhiteSpace(activityTag)
                ? ActivityCards.FirstOrDefault(item =>
                    string.Equals(item.Tag, activityTag, StringComparison.OrdinalIgnoreCase))
                : null;
            SelectActivity(requestedActivity ?? ActivityCards.FirstOrDefault());
        }
        catch (Exception exception)
        {
            if (version == _loadVersion)
                ApplyFailure(ApiFailurePresenter.FromException(exception, "Ui_Workout_Error_Load"));
        }
        finally
        {
            if (version == _loadVersion)
            {
                _hasLoadedSession = true;
                IsBusy = false;
                OnPropertyChanged(nameof(HasNoActivities));
                OnPropertyChanged(nameof(HasActivityChoices));
            }
        }
    }

    [RelayCommand]
    private Task RetryLoadAsync()
    {
        return IsBusy ? Task.CompletedTask : LoadSession(_workoutDayId);
    }

    [RelayCommand]
    private void SelectActivity(WorkoutActivitySectionItem? activity)
    {
        if (IsBusy || activity is null || !ActivityCards.Contains(activity)) return;
        SelectedActivity = activity;
        TotalExercisesInActivity = activity.Exercises.Count;
        RefreshActivityIntro();
        ErrorText = string.Empty;
        Celebration = CelebrationType.None;
        CurrentScreen = SessionScreen.ActivityIntro;
    }

    private void RefreshActivityIntro()
    {
        if (SelectedActivity is null) return;
        ExercisePreviewNames =
            new ObservableCollection<string>(
                SelectedActivity.Exercises.Select((exercise, index) => $"{index + 1}. {exercise.Name}"));
        var targets = string.Join(" ",
            SelectedActivity.Exercises.Select(exercise => exercise.TargetText + " " + exercise.RecommendationText));
        var rpe = Regex.Match(targets, @"RPE\s*(\d+(?:[.,]\d+)?(?:\s*[-–]\s*\d+(?:[.,]\d+)?)?)",
            RegexOptions.IgnoreCase);
        ActivityIntroIntensity = rpe.Success
            ? UiTextCatalog.Format("Workout_Session_TargetRpeFormat", rpe.Groups[1].Value)
            : UiTextCatalog.Get(targets.Contains("steady", StringComparison.OrdinalIgnoreCase)
                ? "Workout_Session_SteadyEffort"
                : "Workout_Session_ComfortableEffort");
        ActivityPrescriptionText = FormatPrescription(SelectedActivity.Prescription);
    }

    private void ApplyFailure(ApiFailureNotice notice)
    {
        ErrorText = notice.MessageKey;
        ErrorReference = notice.ReferenceText;
    }

    private static string FormatPrescription(ActivitySessionPrescriptionOutputV020? prescription)
    {
        if (prescription is null) return string.Empty;
        var lines = new List<string>();
        void AddValue(string key, decimal? value)
        {
            if (value is not null)
                lines.Add(UiTextCatalog.Format(key, value));
        }
        AddValue("Workout_Session_PrescriptionWarmup", prescription.WarmupMinutes);
        AddValue("Workout_Session_PrescriptionMain", prescription.MainMinutes);
        AddValue("Workout_Session_PrescriptionCooldown", prescription.CooldownMinutes);
        var intensityKey = prescription.Intensity switch
        {
            "Easy" => "Workout_Session_PrescriptionEasy",
            "Moderate" => "Workout_Session_PrescriptionModerate",
            "Vigorous" => "Workout_Session_PrescriptionVigorous",
            _ => null
        };
        if (intensityKey is not null) lines.Add(UiTextCatalog.Get(intensityKey));
        if (prescription.IntervalWorkCount is not null && prescription.IntervalWorkSeconds is not null)
            lines.Add(UiTextCatalog.Format("Workout_Session_PrescriptionWorkIntervals",
                prescription.IntervalWorkCount, prescription.IntervalWorkSeconds));
        if (prescription.IntervalRecoveryCount is not null && prescription.IntervalRecoverySeconds is not null)
            lines.Add(UiTextCatalog.Format("Workout_Session_PrescriptionRecoveryIntervals",
                prescription.IntervalRecoveryCount, prescription.IntervalRecoverySeconds));
        return string.Join(Environment.NewLine, lines);
    }

    [RelayCommand]
    private async Task StartActivityAsync()
    {
        if (_pendingCompletionAtUtc is not null)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return;
        }
        if (IsBusy || IsActivityUnsafe || !IsActivityIntro || SelectedActivity is null || _isRestDay) return;
        _appSuspended = false;
        IsBusy = true;
        try
        {
            _plannedSessionId ??= Guid.NewGuid();
            if (_plannedSessionStartedAtUtc == default)
                _plannedSessionStartedAtUtc = DateTime.UtcNow;
            if (!_sessionCreated)
            {
                var started = await _session.StartPlannedSessionAsync(_workoutDayId, _plannedSessionId.Value, _plannedSessionStartedAtUtc);
                _plannedSessionId = started.SessionId;
                _sessionCreated = true;
                _confirmedActiveSeconds = started.ConfirmedActiveSeconds;
                OnPropertyChanged(nameof(CanAbandonPlannedSession));
            }
            await ResumeTimingAsync(_plannedSessionId.Value);
            if (_appSuspended) return;
            ErrorText = string.Empty;
        }
        catch (WorkoutActivityNoLongerSafeException)
        {
            IsActivityUnsafe = true;
            ErrorText = "Workout_Session_ActivityNoLongerSafe";
            return;
        }
        catch (WorkoutHiitHistoryConfirmationRequiredException)
        {
            await ConfirmHiitHistoryAsync();
            return;
        }
        catch (WorkoutTimingIntervalExpiredException)
        {
            ErrorText = "Workout_Session_TimingExpired";
            return;
        }
        catch (WorkoutSessionStartBlockedException exception)
        {
            ErrorText = StartRefusalTextKey(exception.Code);
            return;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ApplyFailure(ApiFailurePresenter.FromException(exception, "Workout_Session_ConnectionRetry"));
            return;
        }
        catch (Exception)
        {
            ErrorText = "Workout_Session_StartFailed";
            return;
        }
        finally { IsBusy = false; }
        if (!_activityTimers.TryGetValue(SelectedActivity, out var timer))
            _activityTimers[SelectedActivity] = timer = new Stopwatch();
        timer.Start();
        if (SelectedActivity.IsTimedActivity)
        {
            // Every non-strength timed activity shares this one running surface: the same phase,
            // clock, pause/resume and summary lifecycle, driven only by that activity's prescription.
            IsTimedActivityRunning = true;
            IsTimedActivityPaused = false;
            CurrentScreen = SessionScreen.TimedActivity;
            StartTimedActivityDisplay();
            return;
        }
        CurrentScreen = SessionScreen.ExerciseFlow;
        SetCurrentExercise(0);
    }

    private const string StartFailedTextKey = "Workout_Session_StartFailed";

    private static string StartRefusalTextKey(string code) => code switch
    {
        WorkoutSessionStartBlockCode.PlannedWorkoutUnavailable => "Workout_Session_StartUnavailable",
        WorkoutSessionStartBlockCode.TimeZoneNotEstablished => "Workout_Session_StartTimeZoneMissing",
        WorkoutSessionStartBlockCode.NotScheduledToday => "Workout_Session_StartNotToday",
        WorkoutSessionStartBlockCode.ActiveSessionOrPreviewOpen => "Workout_Session_StartSessionOrPreviewOpen",
        WorkoutSessionStartBlockCode.PrescriptionChanged => "Workout_Session_StartPrescriptionChanged",
        WorkoutSessionStartBlockCode.SafetyReviewRequired => "Workout_Session_StartSafetyReview",
        WorkoutSessionStartBlockCode.ActiveWorkoutInProgress => "Workout_Session_StartActiveWorkout",
        _ => StartFailedTextKey
    };

    private async Task ConfirmHiitHistoryAsync()
    {
        try
        {
            if (_planning is null || _confirmHiitAsync is null)
            {
                ErrorText = "Workout_Session_HiitConfirmationRequired";
                return;
            }
            var saved = await _planning.ReadAnswersAsync();
            var choice = await _confirmHiitAsync(saved);
            if (choice == HiitConfirmationChoice.Update)
            {
                if (_updateHiitAsync is not null) await _updateHiitAsync();
                return;
            }
            if (choice != HiitConfirmationChoice.Confirm || saved.HiitEligibilityEvidence is null ||
                saved.HiitUnderlyingCardioActivity is null)
                return;
            var challenge = await _planning.IssueHiitCaptureChallengeAsync();
            await _planning.SaveAnswersAsync(new SaveActivityAnswersRequest(saved.Version,
                saved.ActivityEligibilityEvidence, saved.HiitEligibilityEvidence)
            {
                HiitUnderlyingCardioActivity = saved.HiitUnderlyingCardioActivity,
                HiitCaptureChallengeId = challenge.ChallengeId
            });
            ErrorText = "Workout_Session_HiitConfirmedRetry";
        }
        catch (WorkoutPreviewAlreadyOpenException)
        {
            ErrorText = "Workout_Session_HiitPreviewOpen";
        }
        catch (WorkoutPlanningInputsChangedException)
        {
            ErrorText = "Workout_Session_HiitAnswersChanged";
        }
        catch (Exception)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
        }
    }

    [RelayCommand]
    private async Task NextExerciseAsync()
    {
        if (!CanChangeExercise() || CurrentExercise is not { } exercise) return;
        ErrorText = string.Empty;
        Celebration = CelebrationType.None;
        if (!ValidateInputs(exercise)) return;
        var version = _loadVersion;
        var workoutDayId = _workoutDayId;
        IsBusy = true;
        try
        {
            var log = BuildLogInput(exercise);
            if (version != _loadVersion) return;
            await _session.LogAsync(new LogWorkoutSessionRequest(
                workoutDayId, DateOnly.FromDateTime(DateTime.Today), [log], _plannedSessionId));
            if (version != _loadVersion) return;
            var celebration = GetCelebration(exercise, log);
            _savedLogs[exercise.WorkoutDayExerciseId] = log;
            exercise.IsLogged = true;
            _skippedIds.Remove(exercise.WorkoutDayExerciseId);
            if (celebration == CelebrationType.BigPR) _prIds.Add(exercise.WorkoutDayExerciseId);
            RefreshActivityCards();
            AdvanceExercise();
            Celebration = celebration;
            CelebrationText = celebration switch
            {
                CelebrationType.BigPR => UiTextCatalog.Format(
                    "Workout_Session_CelebrationPrFormat", exercise.Name),
                CelebrationType.MediumFirstTime => UiTextCatalog.Format(
                    "Workout_Session_CelebrationFirstFormat", exercise.Name),
                _ => UiTextCatalog.Format("Workout_Session_CelebrationLoggedFormat", exercise.Name)
            };
        }
        catch (Exception)
        {
            if (version == _loadVersion)
                ErrorText = "Ui_Workout_Error_SaveExercise";
        }
        finally
        {
            if (version == _loadVersion) IsBusy = false;
        }
    }

    [RelayCommand]
    private void PreviousExercise()
    {
        if (CanChangeExercise() && CurrentExerciseIndex > 0) SetCurrentExercise(CurrentExerciseIndex - 1);
    }

    [RelayCommand]
    private void JumpToExercise(int index)
    {
        if (CanChangeExercise() && index >= 0 && index < TotalExercisesInActivity) SetCurrentExercise(index);
    }

    [RelayCommand]
    private void SkipExercise()
    {
        if (!CanChangeExercise() || CurrentExercise is null) return;
        // A skip never erases an exercise that was already successfully logged.
        if (!CurrentExercise.IsLogged) _skippedIds.Add(CurrentExercise.WorkoutDayExerciseId);
        Celebration = CelebrationType.None;
        RefreshActivityCards();
        AdvanceExercise();
    }

    [RelayCommand]
    private void ToggleTips()
    {
        IsTipsExpanded = !IsTipsExpanded;
    }

    [RelayCommand]
    private void DismissCelebration()
    {
        Celebration = CelebrationType.None;
    }

    private bool CanChangeExercise()
    {
        return !IsBusy && IsExerciseFlow && !IsSwapPanelVisible && !SwapNeedsReload;
    }

    private void SetCurrentExercise(int index)
    {
        CurrentExerciseIndex = index;
        CurrentExercise = SelectedActivity!.Exercises[index];
        IsTipsExpanded = false;
        ErrorText = string.Empty;
        Celebration = CelebrationType.None;
        RefreshProgress();
    }

    private decimal? ReadTimedActivityDistance() =>
        TryReadTimedActivityDistance(out var distanceKm) ? distanceKm : null;

    private bool TryReadTimedActivityDistance(out decimal? distanceKm)
    {
        distanceKm = null;
        if (!ShowsTimedActivityDistance || string.IsNullOrWhiteSpace(TimedActivityDistanceText))
            return true;
        var normalized = TimedActivityDistanceText.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value) ||
            value < 0m || value > TimedActivityProgress.MaximumDistanceKm)
            return false;
        distanceKm = TimedActivityProgress.NormalizeDistance(value);
        return true;
    }

    private void RefreshProgress()
    {
        ExerciseProgress = new ObservableCollection<SessionExerciseProgress>(
            SelectedActivity!.Exercises.Select((exercise, index) =>
                new SessionExerciseProgress(index, exercise.Name, exercise.IsLogged, index == CurrentExerciseIndex,
                    _skippedIds.Contains(exercise.WorkoutDayExerciseId))));
    }

    private void AdvanceExercise()
    {
        if (CurrentExerciseIndex + 1 < TotalExercisesInActivity)
        {
            SetCurrentExercise(CurrentExerciseIndex + 1);
            return;
        }

        // Tapping the last dot must not silently finish unvisited exercises.
        var pending = SelectedActivity!.Exercises.Select((exercise, index) => (exercise, index))
            .FirstOrDefault(pair =>
                !pair.exercise.IsLogged && !_skippedIds.Contains(pair.exercise.WorkoutDayExerciseId));
        if (pending.exercise is not null)
        {
            SetCurrentExercise(pending.index);
            return;
        }

        ShowSummary();
    }

    private void ShowSummary()
    {
        _timingLoopCancellation?.Cancel();
        _plannedSessionTimer.Stop();
        var activity = SelectedActivity!;
        if (_activityTimers.TryGetValue(activity, out var timer)) timer.Stop();
        var ids = activity.Exercises.Select(exercise => exercise.WorkoutDayExerciseId).ToHashSet();
        var volume = _savedLogs.Where(pair => ids.Contains(pair.Key))
            .Sum(pair => (double)pair.Value.CompletedSets * pair.Value.CompletedReps * (pair.Value.WeightKg ?? 0));
        SummaryTotalVolume = volume >= 1000 ? $"{volume / 1000:0.#} t" : $"{volume:0.#} kg";
        SummaryPrCount = _prIds.Count(ids.Contains);
        SummaryDuration = timer is null || timer.Elapsed.TotalMinutes < 1
            ? UiTextCatalog.Get("Workout_Session_SummaryDurationUnderMinute")
            : UiTextCatalog.Format("Workout_Session_SummaryDurationMinutesFormat",
                (int)timer.Elapsed.TotalMinutes);
        SummaryEffortText = string.Empty;
        var logged = activity.Exercises.Count(exercise => exercise.IsLogged);
        IsActivityComplete = logged == ids.Count;
        SummaryTitle = UiTextCatalog.Get(IsActivityComplete
            ? "Workout_Session_SummaryCompleteTitle"
            : "Workout_Session_SummaryReviewedTitle");
        SummaryStatusText = UiTextCatalog.Format("Workout_Session_SummaryLoggedFormat", logged, ids.Count) +
                            (ids.Any(_skippedIds.Contains)
                                ? UiTextCatalog.Format("Workout_Session_SummarySkippedFormat",
                                    ids.Count(_skippedIds.Contains))
                                : string.Empty);
        ErrorText = string.Empty;
        RefreshProgress();
        CurrentScreen = SessionScreen.ActivitySummary;
    }

    [RelayCommand]
    private async Task FinishActivityAsync()
    {
        if (IsBusy || SwapNeedsReload) return;
        // The summary can follow a completion that already committed; only re-finish an active session.
        if (_plannedSessionId is not null && !await CompletePlannedSessionAsync()) return;
        StopTimedActivityDisplay();
        PauseActivity();
        CloseSwapPanel();
        RefreshActivityCards();
        SelectedActivity = null;
        CurrentExercise = null;
        SummaryEffortText = string.Empty;
        SummaryDistanceText = string.Empty;
        Celebration = CelebrationType.None;
        ErrorText = string.Empty;
        await _navigateAsync("..");
    }

    [RelayCommand]
    private async Task FinishTimedActivityAsync()
    {
        if (IsBusy) return;
        // A retained completion identity means the previous finish was not acknowledged. Retrying
        // it with the frozen body is safe; the guard against a second identity stays inside
        // CompletePlannedSessionCoreAsync.
        if (!IsTimedActivityRunning && !IsTimedActivityPaused) return;
        decimal? requestedDistance = null;
        if (_pendingCompletionAtUtc is null && !TryReadTimedActivityDistance(out requestedDistance))
        {
            ErrorText = "Workout_Session_DistanceInvalid";
            return;
        }
        var measuredSeconds = CurrentTimedElapsedSeconds;
        var summaryRpe = _pendingCompletionRpe ?? TimedActivityRpe;
        var summaryDistance = _pendingCompletionDistanceKm ?? requestedDistance;
        if (!await CompletePlannedSessionAsync())
        {
            // The finish was not acknowledged. Keep the draft, the confirmed time and the retained
            // completion identity so a retry reuses them instead of starting a second session.
            if (CurrentScreen == SessionScreen.TimedActivity && _plannedSessionId.HasValue)
            {
                IsTimedActivityRunning = false;
                IsTimedActivityPaused = true;
                PauseActivity();
                RefreshTimedActivityDisplay();
            }
            return;
        }
        StopTimedActivityDisplay();
        PauseActivity();
        IsTimedActivityRunning = false;
        IsTimedActivityPaused = false;
        ShowTimedActivitySummary(measuredSeconds, summaryRpe, summaryDistance);
    }

    private void ShowTimedActivitySummary(int measuredSeconds, int? rpe, decimal? distanceKm)
    {
        SummaryTotalVolume = UiTextCatalog.Get("Workout_Session_NotApplicable");
        SummaryPrCount = 0;
        SummaryDuration = measuredSeconds >= 60
            ? UiTextCatalog.Format("Workout_Session_TimedDurationMinutes",
                (int)TimeSpan.FromSeconds(measuredSeconds).TotalMinutes)
            : UiTextCatalog.Get("Workout_Session_TimedDurationUnderMinute");
        SummaryEffortText = rpe is { } rating
            ? UiTextCatalog.Format("Workout_Session_SummaryEffortFormat", rating)
            : UiTextCatalog.Get("Workout_Session_SummaryEffortNotRated");
        SummaryDistanceText = distanceKm is { } km
            ? UiTextCatalog.Format("Workout_Session_SummaryDistanceFormat",
                km.ToString("0.##", CultureInfo.CurrentCulture))
            : string.Empty;
        SummaryTitle = "Workout_Session_TimedSummaryTitle";
        SummaryStatusText = "Workout_Session_TimedSummaryStatus";
        IsActivityComplete = true;
        ErrorText = string.Empty;
        CurrentScreen = SessionScreen.ActivitySummary;
    }

    [RelayCommand]
    private async Task PauseTimedActivityAsync()
    {
        if (!CanPauseTimedActivity) return;
        IsBusy = true;
        try
        {
            if (!await SuspendTimingAsync()) return;
            IsTimedActivityRunning = false;
            IsTimedActivityPaused = true;
            StopTimedActivityDisplay();
            RefreshTimedActivityDisplay();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResumeTimedActivityAsync()
    {
        if (_pendingCompletionAtUtc is not null)
        {
            // A finish was already submitted. Retry that finish instead of resuming past it.
            ErrorText = "Workout_Session_ConnectionRetry";
            return;
        }
        if (!CanResumeTimedActivity || _plannedSessionId is not { } sessionId) return;
        // An explicit resume means the page is visible again. Clear the suspension flag that backgrounding
        // set, otherwise ResumeTimingAsync reports success but refuses to restart the local clock and the
        // paused surface keeps a Resume button that can never recover. A later suspension sets it again.
        _appSuspended = false;
        IsBusy = true;
        try
        {
            await ResumeTimingAsync(sessionId);
            if (_appSuspended) return;
            IsTimedActivityPaused = false;
            IsTimedActivityRunning = true;
            ErrorText = string.Empty;
            StartTimedActivityDisplay();
        }
        catch (WorkoutActivityNoLongerSafeException)
        {
            IsActivityUnsafe = true;
            ErrorText = "Workout_Session_ActivityNoLongerSafe";
        }
        catch (WorkoutTimingIntervalExpiredException)
        {
            ErrorText = "Workout_Session_TimingExpired";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ApplyFailure(ApiFailurePresenter.FromException(exception, "Workout_Session_ConnectionRetry"));
        }
        catch (Exception)
        {
            ErrorText = "Workout_Session_TimingFailed";
        }
        finally { IsBusy = false; }
    }

    private int CurrentTimedElapsedSeconds =>
        _confirmedActiveSeconds + (int)_plannedSessionTimer.Elapsed.TotalSeconds;

    private void StartTimedActivityDisplay()
    {
        StopTimedActivityDisplay();
        RefreshTimedActivityDisplay();
        var cancellation = new CancellationTokenSource();
        _timedDisplayCancellation = cancellation;
        _ = RunTimedActivityDisplayAsync(cancellation.Token);
    }

    private void StopTimedActivityDisplay()
    {
        _timedDisplayCancellation?.Cancel();
        _timedDisplayCancellation?.Dispose();
        _timedDisplayCancellation = null;
    }

    private async Task RunTimedActivityDisplayAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
                MainThread.BeginInvokeOnMainThread(RefreshTimedActivityDisplay);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void RefreshTimedActivityDisplay()
    {
        var prescription = SelectedActivity?.Prescription;
        var state = TimedActivityProgress.Describe(CurrentTimedElapsedSeconds,
            prescription?.WarmupMinutes, prescription?.MainMinutes, prescription?.CooldownMinutes);
        TimedActivityElapsedText = FormatClock(state.ElapsedSeconds);
        TimedActivityPhaseKey = state.PhaseKey;
        TimedActivityRemainingText = state.RemainingSeconds is { } remaining ? FormatClock(remaining) : string.Empty;
        TimedActivityProgressValue = state.Progress;
    }

    private static string FormatClock(int seconds)
    {
        var clamped = Math.Max(0, seconds);
        return clamped >= 3600
            ? $"{clamped / 3600}:{clamped % 3600 / 60:00}:{clamped % 60:00}"
            : $"{clamped / 60:00}:{clamped % 60:00}";
    }

    [RelayCommand]
    private async Task CompleteSessionAsync()
    {
        // A timed activity is completed before its summary, so the summary's end action only
        // needs to return. Strength sessions still complete here and only leave on acknowledgment.
        if (_plannedSessionId is null)
        {
            await _navigateAsync("..");
            return;
        }
        if (await CompletePlannedSessionAsync())
            await _navigateAsync("..");
    }

    private async Task<bool> CompletePlannedSessionAsync(int? timedActivitySeconds = null)
    {
        if (!await _completionGate.WaitAsync(NoCompletionWaitMilliseconds)) return false;
        try { return await CompletePlannedSessionCoreAsync(timedActivitySeconds); }
        finally { _completionGate.Release(); }
    }

    private async Task<bool> CompletePlannedSessionCoreAsync(int? timedActivitySeconds = null)
    {
        if (IsBusy || _plannedSessionId is not { } sessionId) return false;
        _timingLoopCancellation?.Cancel();
        _plannedSessionTimer.Stop();
        try
        {
            await CheckpointTimingAsync(pause: true);
        }
        catch (WorkoutTimingIntervalExpiredException)
        {
            ErrorText = "Workout_Session_TimingExpired";
            IsTimedActivityRunning = false;
            CurrentScreen = SessionScreen.ActivityIntro;
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return false;
        }
        catch (Exception)
        {
            ErrorText = "Workout_Session_TimingFailed";
            return false;
        }
        _pendingCompletionSeconds ??= _confirmedActiveSeconds;
        if (_pendingCompletionSeconds <= 0)
        {
            ErrorText = "Ui_Workout_Error_MinimumDuration";
            _pendingCompletionSeconds = null;
            IsTimedActivityRunning = false;
            CurrentScreen = SessionScreen.ActivityIntro;
            return false;
        }
        _pendingCompletionAtUtc ??= DateTime.UtcNow;
        // Freeze the optional actuals with the completion identity: a retry must send the same body
        // the server may already have accepted, not whatever the controls show later.
        _pendingCompletionRpe ??= SelectedActivity?.IsTimedActivity == true
            ? TimedActivityProgress.NormalizeRpe(TimedActivityRpeValue)
            : null;
        _pendingCompletionDistanceKm ??= ReadTimedActivityDistance();
        OnPropertyChanged(nameof(CanAbandonPlannedSession));
        IsBusy = true;
        try
        {
            await _session.FinishPlannedSessionAsync(sessionId, _pendingCompletionAtUtc.Value,
                _pendingCompletionSeconds.Value, _pendingCompletionRpe, _pendingCompletionDistanceKm);
            _plannedSessionId = null;
            OnPropertyChanged(nameof(CanAbandonPlannedSession));
            _plannedSessionStartedAtUtc = default;
            _plannedSessionTimer.Reset();
            _sessionCreated = false;
            _confirmedActiveSeconds = 0;
            _timingIntervalId = null;
            _pendingResumeOperationId = null;
            _pendingTimingCheckpoint = null;
            _pendingCompletionAtUtc = null;
            _pendingCompletionSeconds = null;
            _pendingCompletionRpe = null;
            _pendingCompletionDistanceKm = null;
            ErrorText = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return false;
        }
        catch (Exception)
        {
            ErrorText = "Workout_Session_FinishFailed";
            return false;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AbandonPlannedSession()
    {
        if (_pendingCompletionAtUtc is not null)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return;
        }
        if (IsBusy || _plannedSessionId is null) return;
        // Abandon is destructive for the session record, so it always needs an explicit confirmation.
        ErrorText = string.Empty;
        IsAbandonConfirmationVisible = true;
    }

    [RelayCommand]
    private void CancelAbandonPlannedSession()
    {
        // Cancelling keeps the active state and any confirmed time; nothing is sent to the server.
        if (IsBusy) return;
        IsAbandonConfirmationVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmAbandonPlannedSessionAsync()
    {
        if (_pendingCompletionAtUtc is not null)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return;
        }
        if (IsBusy) return;
        if (_plannedSessionId is not { } sessionId)
        {
            // A previous request already settled the session; just close the confirmation.
            IsAbandonConfirmationVisible = false;
            return;
        }

        IsBusy = true;
        try
        {
            if (_abandonOutcomeUnknown)
            {
                // An earlier attempt may have landed. Reconcile before sending a second abandon so a
                // lost response can never duplicate or mis-complete the session.
                var existing = await _session.GetPlannedSessionAsync(_workoutDayId);
                if (existing is null || existing.IsCompleted || existing.IsCanceled)
                {
                    await SettleAbandonedSessionAsync();
                    return;
                }
            }

            await _session.AbandonPlannedSessionAsync(sessionId);
            await SettleAbandonedSessionAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _abandonOutcomeUnknown = true;
            ErrorText = "Workout_Session_AbandonOutcomeUnknown";
        }
        catch (Exception)
        {
            _abandonOutcomeUnknown = true;
            ErrorText = "Workout_Session_AbandonFailed";
        }
        finally { IsBusy = false; }
    }

    private async Task SettleAbandonedSessionAsync()
    {
        // Every exit path clears the timer, pending work and overlays before one bounded return.
        StopTimedActivityDisplay();
        _timingLoopCancellation?.Cancel();
        StopRestTimer();
        PauseActivity();
        _activityTimers.Clear();
        _plannedSessionTimer.Reset();
        _plannedSessionId = null;
        _sessionCreated = false;
        _timingIntervalId = null;
        _pendingTimingCheckpoint = null;
        _pendingResumeOperationId = null;
        _pendingCompletionAtUtc = null;
        _pendingCompletionSeconds = null;
        _pendingCompletionRpe = null;
        _pendingCompletionDistanceKm = null;
        _confirmedActiveSeconds = 0;
        _plannedSessionStartedAtUtc = default;
        _abandonOutcomeUnknown = false;
        IsTimedActivityRunning = false;
        IsTimedActivityPaused = false;
        IsAbandonConfirmationVisible = false;
        ErrorText = string.Empty;
        OnPropertyChanged(nameof(CanAbandonPlannedSession));
        await _navigateAsync("..");
    }

    [RelayCommand]
    private async Task FinishSessionAsync()
    {
        if (IsBusy) return;
        StopRestTimer();
        if (!await SuspendTimingAsync()) return;
        await _navigateAsync("..");
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (IsBusy) return;
        if (_pendingCompletionAtUtc is not null)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return;
        }
        if (SwapNeedsReload)
        {
            await FinishSessionAsync();
            return;
        }

        if (IsSwapPanelVisible)
        {
            CloseSwapPanel();
            return;
        }

        if (IsExerciseFlow)
        {
            if (!await SuspendTimingAsync()) return;
            Celebration = CelebrationType.None;
            ErrorText = string.Empty;
            CurrentScreen = SessionScreen.ActivityIntro;
            return;
        }

        if (!await SuspendTimingAsync()) return;
        IsTimedActivityRunning = false;
        IsTimedActivityPaused = false;
        IsAbandonConfirmationVisible = false;
        StopTimedActivityDisplay();
        SelectedActivity = null;
        CurrentExercise = null;
        await _navigateAsync("..");
    }

    private async Task ResumeTimingAsync(Guid sessionId)
    {
        await _timingGate.WaitAsync();
        try
        {
            await FlushPendingTimingCheckpointAsync(sessionId);
            _pendingResumeOperationId ??= Guid.NewGuid();
            var snapshot = await _session.ResumePlannedTimingAsync(sessionId, _pendingResumeOperationId.Value);
            _pendingResumeOperationId = null;
            _timingIntervalId = snapshot.TimingIntervalId ?? throw new InvalidOperationException("Workout timing could not resume.");
            _confirmedActiveSeconds = snapshot.ConfirmedActiveSeconds;
            _plannedSessionTimer.Restart();
            if (_appSuspended)
            {
                _plannedSessionTimer.Stop();
                return;
            }
            _timingLoopCancellation?.Cancel();
            _timingLoopCancellation = new CancellationTokenSource();
            _ = CheckpointTimingLoopAsync(_timingLoopCancellation.Token);
        }
        finally { _timingGate.Release(); }
    }

    private async Task CheckpointTimingAsync(bool pause)
    {
        await _timingGate.WaitAsync();
        try
        {
            if (_plannedSessionId is not { } sessionId) return;
            await FlushPendingTimingCheckpointAsync(sessionId);
            if (_timingIntervalId is not { } intervalId) return;
            var measuredSeconds = (int)_plannedSessionTimer.Elapsed.TotalSeconds;
            _pendingTimingCheckpoint = new PendingTimingCheckpoint(Guid.NewGuid(), intervalId, measuredSeconds, pause);
            await FlushPendingTimingCheckpointAsync(sessionId);
        }
        finally { _timingGate.Release(); }
    }

    private async Task FlushPendingTimingCheckpointAsync(Guid sessionId)
    {
        if (_pendingTimingCheckpoint is not { } pending) return;
        MuscleCuties.UseCases.Features.Workout.ActiveSession.ActiveWorkoutSessionSnapshot snapshot;
        try
        {
            snapshot = await _session.CheckpointPlannedTimingAsync(sessionId, pending.OperationId,
                pending.IntervalId, pending.MeasuredSeconds, pending.Pause);
        }
        catch (WorkoutTimingIntervalExpiredException)
        {
            _pendingTimingCheckpoint = null;
            _timingIntervalId = null;
            _plannedSessionTimer.Reset();
            throw;
        }
        _confirmedActiveSeconds = snapshot.ConfirmedActiveSeconds;
        _timingIntervalId = snapshot.TimingIntervalId;
        _pendingTimingCheckpoint = null;
        if (pending.Pause) _plannedSessionTimer.Reset();
    }

    private async Task CheckpointTimingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(TimingCheckpointIntervalSeconds));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await CheckpointTimingAsync(pause: false);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (ErrorText is "Workout_Session_ConnectionRetry" or "Workout_Session_TimingFailed")
                            ErrorText = string.Empty;
                    });
                }
                catch (Exception exception)
                {
                    _plannedSessionTimer.Stop();
                    var key = exception is WorkoutTimingIntervalExpiredException ? "Workout_Session_TimingExpired" :
                        exception is HttpRequestException or TaskCanceledException
                            ? "Workout_Session_ConnectionRetry" : "Workout_Session_TimingFailed";
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ErrorText = key;
                        IsTimedActivityRunning = false;
                        if (CurrentScreen == SessionScreen.ExerciseFlow)
                            CurrentScreen = SessionScreen.ActivityIntro;
                    });
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    public async Task<bool> SuspendTimingAsync()
    {
        _timingLoopCancellation?.Cancel();
        PauseActivity();
        try
        {
            await CheckpointTimingAsync(pause: true);
            return true;
        }
        catch (WorkoutTimingIntervalExpiredException)
        {
            ErrorText = "Workout_Session_TimingExpired";
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return false;
        }
        catch (Exception)
        {
            ErrorText = "Workout_Session_TimingFailed";
            return false;
        }
    }

    public async Task SuspendForAppBackgroundAsync()
    {
        _appSuspended = true;
        var wasTimedActivity = IsTimedActivityScreen;
        var wasExerciseFlow = IsExerciseFlow;
        var suspend = SuspendTimingAsync();
        if ((wasTimedActivity || wasExerciseFlow) && _pendingCompletionAtUtc is null)
        {
            IsTimedActivityRunning = false;
            if (wasTimedActivity)
            {
                // Keep the running surface visible; the user resumes from where they left off.
                IsTimedActivityPaused = true;
                StopTimedActivityDisplay();
                RefreshTimedActivityDisplay();
            }
            else
            {
                CurrentScreen = SessionScreen.ActivityIntro;
            }
        }
        await suspend;
    }

    private void PauseActivity()
    {
        _plannedSessionTimer.Stop();
        if (SelectedActivity is not null && _activityTimers.TryGetValue(SelectedActivity, out var timer)) timer.Stop();
    }

    [RelayCommand]
    private async Task OpenSwapPanelAsync()
    {
        if (!CanChangeExercise() || CurrentExercise is not { UsesStrengthMetrics: true } exercise) return;
        var version = ++_swapVersion;
        IsSwapPanelVisible = true;
        IsSwapLoading = true;
        SwapErrorText = string.Empty;
        SwapTargetName = exercise.Name;
        SwapCandidates = [];
        try
        {
            var candidates = await _session.GetSwapCandidatesAsync(exercise.WorkoutDayExerciseId);
            if (version == _swapVersion)
                SwapCandidates = new ObservableCollection<ExerciseSwapOption>(candidates.Select(candidate =>
                    new ExerciseSwapOption
                    {
                        ExerciseId = candidate.ExerciseId,
                        Name = candidate.Name,
                        MuscleMatch = candidate.MuscleMatch,
                        Equipment = candidate.Equipment,
                        PrimaryMuscles = candidate.PrimaryMuscles
                    }));
        }
        catch
        {
            if (version == _swapVersion) SwapErrorText = "Ui_Workout_Error_SwapLoad";
        }
        finally
        {
            if (version == _swapVersion) IsSwapLoading = false;
        }
    }

    [RelayCommand]
    private void SelectSwapCandidate(ExerciseSwapOption? option)
    {
        if (IsBusy || SwapNeedsReload || option is null || !SwapCandidates.Contains(option)) return;
        foreach (var candidate in SwapCandidates) candidate.IsSelected = candidate == option;
    }

    [RelayCommand]
    private async Task ConfirmSwapAsync()
    {
        var selected = SwapCandidates.FirstOrDefault(candidate => candidate.IsSelected);
        if (IsBusy || IsSwapLoading || !IsSwapPanelVisible ||
            (!SwapNeedsReload && selected is null) || CurrentExercise is null) return;
        var version = _loadVersion;
        var workoutDayId = _workoutDayId;
        IsBusy = true;
        SwapErrorText = string.Empty;
        try
        {
            var targetId = CurrentExercise.WorkoutDayExerciseId;
            if (version != _loadVersion) return;
            if (!SwapNeedsReload)
            {
                // The committed replacement must be reloaded before old metrics can be submitted.
                SwapNeedsReload = true;
                await _session.SwapAsync(new SwapWorkoutExerciseRequest(targetId, selected!.ExerciseId, true));
            }

            var detail = await _session.GetDetailAsync(workoutDayId);
            if (version != _loadVersion) return;
            var replacement = GetActivities(detail).SelectMany(activity => activity.Exercises)
                .First(exercise => exercise.WorkoutDayExerciseId == targetId);
            SelectedActivity!.Exercises[CurrentExerciseIndex] = replacement;
            _skippedIds.Remove(targetId);
            _prIds.Remove(targetId);
            _savedLogs.Remove(targetId);
            if (replacement.IsLogged) _savedLogs[targetId] = BuildLogInput(replacement);
            RefreshActivityCards();
            RefreshActivityIntro();
            SetCurrentExercise(CurrentExerciseIndex);
            if (version == _loadVersion) ResetSwapPanel();
        }
        catch
        {
            if (version == _loadVersion)
                SwapErrorText = "Ui_Workout_Error_SwapChanged";
        }
        finally
        {
            if (version == _loadVersion) IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseSwapPanel()
    {
        if (IsBusy || SwapNeedsReload) return;
        ResetSwapPanel();
    }

    [RelayCommand]
    public async Task OpenMovementNoteAsync()
    {
        if (CurrentExercise is not { WorkoutDayExerciseId: > 0 } exercise) return;

        MovementNoteErrorText = string.Empty;
        MovementNoteStatusText = string.Empty;
        IsMovementNoteOpen = true;
        IsMovementNoteBusy = true;
        try
        {
            var noteDto = await _session.GetMovementNoteAsync(exercise.WorkoutDayExerciseId);
            MovementNoteText = noteDto?.Notes ?? string.Empty;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[WorkoutSession] Failed to load movement note: {ex.Message}");
            MovementNoteErrorText = UiTextCatalog.Get("Workout_Session_NoteSaveFailed");
        }
        finally
        {
            IsMovementNoteBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveMovementNoteAsync()
    {
        if (CurrentExercise is not { WorkoutDayExerciseId: > 0 } exercise || IsMovementNoteBusy)
            return;

        MovementNoteErrorText = string.Empty;
        MovementNoteStatusText = string.Empty;
        IsMovementNoteBusy = true;
        try
        {
            await _session.SaveMovementNoteAsync(new SaveWorkoutMovementNoteRequest(
                exercise.WorkoutDayExerciseId,
                MovementNoteText));

            MovementNoteStatusText = UiTextCatalog.Get("Workout_Session_NoteSaved");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[WorkoutSession] Failed to save movement note: {ex.Message}");
            MovementNoteErrorText = UiTextCatalog.Get("Workout_Session_NoteSaveFailed");
        }
        finally
        {
            IsMovementNoteBusy = false;
        }
    }

    [RelayCommand]
    public async Task ClearMovementNoteAsync()
    {
        MovementNoteText = string.Empty;
        await SaveMovementNoteAsync();
    }

    [RelayCommand]
    public void CloseMovementNote()
    {
        IsMovementNoteOpen = false;
        MovementNoteErrorText = string.Empty;
        MovementNoteStatusText = string.Empty;
    }

    private void ResetSwapPanel()
    {
        ++_swapVersion;
        SwapNeedsReload = false;
        IsSwapPanelVisible = false;
        IsSwapLoading = false;
        SwapCandidates = [];
        SwapTargetName = string.Empty;
        SwapErrorText = string.Empty;
    }

    private void RefreshActivityCards()
    {
        // The shared card model has init-only summary fields. Rebuild only its wrapper,
        // preserving exercise objects, edits, selection and elapsed time.
        ActivityCards = new ObservableCollection<WorkoutActivitySectionItem>(ActivityCards.Select(activity =>
        {
            var count = activity.Exercises.Count;
            var skipped = activity.Exercises.Count(exercise => _skippedIds.Contains(exercise.WorkoutDayExerciseId));
            var updated = new WorkoutActivitySectionItem
            {
                OrderIndex = activity.OrderIndex,
                TotalActivities = activity.TotalActivities,
                Tag = activity.Tag,
                Title = activity.Title,
                Subtitle = activity.Subtitle,
                MetricText = count == 1
                    ? UiTextCatalog.Get("Workout_Session_ExerciseCountOne")
                    : UiTextCatalog.Format("Workout_Session_ExerciseCountFormat", count),
                SummaryText = skipped > 0
                    ? UiTextCatalog.Format("Workout_Session_SummaryStatusSkippedFormat",
                        activity.Exercises.Count(exercise => exercise.IsLogged), count, skipped)
                    : UiTextCatalog.Format("Workout_Session_SummaryStatusFormat",
                        activity.Exercises.Count(exercise => exercise.IsLogged), count),
                ActivityBackground = activity.ActivityBackground,
                Prescription = activity.Prescription,
                ActivityType = activity.ActivityType,
                ActivityTextColor = activity.ActivityTextColor,
                Exercises = activity.Exercises
            };
            if (_activityTimers.Remove(activity, out var timer)) _activityTimers[updated] = timer;
            if (SelectedActivity == activity) SelectedActivity = updated;
            return updated;
        }));
    }

    private static IReadOnlyList<WorkoutActivitySectionItem> GetActivities(WorkoutSessionDetailDto detail)
    {
        return WorkoutSessionPresentationMapper.MapActivities(detail);
    }

    private static string BuildPhaseNote(CyclePhase phase)
    {
        _ = phase;
        return "Workout_Session_CycleContextNote";
    }

    private CelebrationType GetCelebration(WorkoutExerciseItem exercise, WorkoutExerciseLogDto log)
    {
        var previous = Regex.Match(exercise.PreviousText, @"(\d+(?:[.,]\d+)?)\s*kg", RegexOptions.IgnoreCase);
        var previousWeight = previous.Success ? ParseOptionalFloat(previous.Groups[1].Value) :
            exercise.PreviousText.Contains("bodyweight", StringComparison.OrdinalIgnoreCase) ? 0f : null;
        // Updating a log cannot repeatedly celebrate the same weight.
        if (_savedLogs.TryGetValue(exercise.WorkoutDayExerciseId, out var saved))
            previousWeight = Math.Max(previousWeight ?? 0, saved.WeightKg ?? 0);
        if (exercise.UsesStrengthMetrics && exercise.UsesWeight && previousWeight.HasValue &&
            log.WeightKg > previousWeight)
            return CelebrationType.BigPR;
        if (!exercise.IsLogged && (string.IsNullOrWhiteSpace(exercise.PreviousText) ||
                                   exercise.PreviousText.Equals("No previous log", StringComparison.OrdinalIgnoreCase)))
            return CelebrationType.MediumFirstTime;
        return CelebrationType.SmallCheckmark;
    }

    private bool ValidateInputs(WorkoutExerciseItem exercise)
    {
        if (exercise.UsesStrengthMetrics && (ParsePositiveInt(exercise.LoggedSetsText) == 0 ||
                                             ParsePositiveInt(exercise.LoggedRepsText) == 0))
            ErrorText = "Ui_Workout_Error_SetsReps";
        else if (exercise.UsesStrengthMetrics && exercise.UsesWeight &&
                 ParseOptionalFloat(exercise.LoggedWeightText) is null)
            ErrorText = "Ui_Workout_Error_Weight";
        else if (exercise.UsesDurationMetric && ParseDurationSeconds(exercise.LoggedDurationMinutesText) is null)
            ErrorText = "Ui_Workout_Error_Duration";
        else if ((exercise.UsesDistanceMetric && InvalidOptionalFloat(exercise.LoggedDistanceKmText)) ||
                 (exercise.UsesHeartRateMetric && InvalidOptionalInt(exercise.LoggedHeartRateText)) ||
                 (exercise.UsesPowerMetric && InvalidOptionalInt(exercise.LoggedPowerWattsText)) ||
                 (exercise.UsesCadenceMetric && InvalidOptionalInt(exercise.LoggedCadenceRpmText)) ||
                 (exercise.UsesPaceMetric && !string.IsNullOrWhiteSpace(exercise.LoggedPaceText) &&
                  ParsePace(exercise.LoggedPaceText) is null) ||
                 (exercise.UsesEffortMetric && !string.IsNullOrWhiteSpace(exercise.LoggedEffortText) &&
                  ParsePositiveInt(exercise.LoggedEffortText) is not (>= 1 and <= 10)))
            ErrorText = "Workout_Session_OptionalMetricsError";
        return !HasError;
    }

    private static bool InvalidOptionalFloat(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && ParseOptionalFloat(value) is null;
    }

    private static bool InvalidOptionalInt(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && ParsePositiveInt(value) == 0;
    }

    private static int ParsePositiveInt(string? value)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0;
    }

    private static int? ParseOptionalInt(string? value)
    {
        return ParsePositiveInt(value) is > 0 and var parsed ? parsed : null;
    }

    private static float? ParseOptionalFloat(string? value)
    {
        if ((float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) ||
             float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) &&
            float.IsFinite(parsed) && parsed >= 0)
            return parsed;
        return null;
    }

    private static int? ParseDurationSeconds(string? value)
    {
        return ParseOptionalFloat(value) is > 0 and var minutes && minutes * 60d <= int.MaxValue
            ? Math.Max(1, (int)Math.Round(minutes * 60d))
            : null;
    }

    private static int? ParsePace(string? value)
    {
        var parts = value?.Split(':', StringSplitOptions.TrimEntries);
        if (parts is { Length: 2 })
            return int.TryParse(parts[0], out var minutes) && minutes >= 0 && minutes <= int.MaxValue / 60 - 1 &&
                   int.TryParse(parts[1], out var seconds) && seconds is >= 0 and < 60 && minutes * 60 + seconds > 0
                ? minutes * 60 + seconds
                : null;
        return ParseDurationSeconds(value);
    }

    private static WorkoutExerciseLogDto BuildLogInput(WorkoutExerciseItem item)
    {
        var observedSets = ObservedSetInputMapping.Build(
            item.UsesStrengthMetrics && item.SetsWereEdited,
            item.Sets.Select(set => new EditableObservedSet(
                set.SetNumber,
                set.RepsCompleted,
                item.UsesWeight ? (decimal)set.WeightKg : 0m,
                set.Rir is >= 0 and <= 10 ? (int)Math.Round(set.Rir.Value) : null,
                set.TechniqueAcceptable,
                set.IsWarmup)));
        var completedSets = observedSets is { Count: > 0 }
            ? observedSets.Count
            : item.UsesStrengthMetrics ? ParsePositiveInt(item.LoggedSetsText) : 0;
        var completedReps = observedSets is { Count: > 0 }
            ? (int)Math.Round(observedSets.Average(set => set.RepsCompleted))
            : item.UsesStrengthMetrics ? ParsePositiveInt(item.LoggedRepsText) : 0;
        var completedWeight = observedSets is { Count: > 0 }
            ? (float?)observedSets.Max(set => set.LoadKg)
            : item.UsesStrengthMetrics && item.UsesWeight ? ParseOptionalFloat(item.LoggedWeightText) : 0f;

        return new WorkoutExerciseLogDto(
            item.WorkoutDayExerciseId, item.ExerciseId,
            completedSets, completedReps, completedWeight,
            item.UsesDurationMetric ? ParseDurationSeconds(item.LoggedDurationMinutesText) : null,
            item.UsesDistanceMetric ? ParseOptionalFloat(item.LoggedDistanceKmText) : null,
            item.UsesHeartRateMetric ? ParseOptionalInt(item.LoggedHeartRateText) : null,
            item.UsesPaceMetric ? ParsePace(item.LoggedPaceText) : null,
            item.UsesPowerMetric ? ParseOptionalInt(item.LoggedPowerWattsText) : null,
            item.UsesCadenceMetric ? ParseOptionalInt(item.LoggedCadenceRpmText) : null,
            item.UsesEffortMetric && ParsePositiveInt(item.LoggedEffortText) is >= 1 and <= 10 and var effort
                ? effort
                : null,
            ObservedSets: observedSets);
    }
}
