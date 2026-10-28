using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

public enum SessionScreen { DayOverview, ActivityIntro, ExerciseFlow, ActivitySummary }

public enum CelebrationType { None, SmallCheckmark, MediumFirstTime, BigPR }
public enum HiitConfirmationChoice { Cancel, Confirm, Update }

public sealed record SessionExerciseProgress(int Index, string Name, bool IsLogged, bool IsCurrent, bool IsSkipped)
{
    public string AccessibilityText => $"Exercise {Index + 1}: {Name}, " +
                                       (IsCurrent ? "current, " : string.Empty) +
                                       (IsLogged ? "logged" : IsSkipped ? "skipped" : "pending");
}

public partial class WorkoutSessionViewModel : ObservableObject
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
    [NotifyPropertyChangedFor(nameof(IsDayOverview), nameof(IsActivityIntro), nameof(IsExerciseFlow),
        nameof(IsActivitySummary))]
    private SessionScreen _currentScreen;

    [ObservableProperty] private string _cyclePhaseNote = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDoseAdjustmentNote))]
    private string _doseAdjustmentNote = string.Empty;
    public bool HasDoseAdjustmentNote => !string.IsNullOrWhiteSpace(DoseAdjustmentNote);
    [ObservableProperty] private string _dayTitle = "Workout";
    [ObservableProperty] private ObservableCollection<string> _sessionHistory = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError), nameof(HasNoActivities),
        nameof(HasActivityChoices))]
    private string _errorText = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _exercisePreviewNames = [];
    [ObservableProperty] private ObservableCollection<SessionExerciseProgress> _exerciseProgress = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanInteract), nameof(CanAbandonPlannedSession), nameof(HasNoActivities),
        nameof(HasActivityChoices))]
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
    [ObservableProperty] private WorkoutActivitySectionItem? _selectedActivity;
    [ObservableProperty] private string _summaryDuration = "<1 min";
    [ObservableProperty] private int _summaryPrCount;
    [ObservableProperty] private string _summaryStatusText = string.Empty;
    [ObservableProperty] private string _summaryTitle = "Activity complete";
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

    private int _workoutDayId;
    private bool _isRestDay;
    private Guid? _plannedSessionId;
    private DateTime _plannedSessionStartedAtUtc;
    private DateTime? _pendingCompletionAtUtc;
    private int? _pendingCompletionSeconds;
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
    private sealed record PendingTimingCheckpoint(Guid OperationId, Guid IntervalId, int MeasuredSeconds, bool Pause);
    [ObservableProperty] private bool _isTimedActivityRunning;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanStartActivity))]
    private bool _isActivityUnsafe;

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

    public bool IsDayOverview => CurrentScreen == SessionScreen.DayOverview;
    public int WorkoutDayId => _workoutDayId;
    public bool IsActivityIntro => CurrentScreen == SessionScreen.ActivityIntro;
    public bool IsExerciseFlow => CurrentScreen == SessionScreen.ExerciseFlow;
    public bool IsActivitySummary => CurrentScreen == SessionScreen.ActivitySummary;
    public bool CanAbandonPlannedSession => _sessionCreated && _plannedSessionId.HasValue && !IsBusy && _pendingCompletionAtUtc is null;
    public bool CanStartActivity => !IsActivityUnsafe;
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
            ErrorText = ex.Message;
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
    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public bool HasNoActivities => !IsBusy && !HasError && ActivityCards.Count == 0;
    public bool HasActivityChoices => !IsBusy && !HasError && ActivityCards.Count > 0;
    public bool HasSwapCandidates => SwapCandidates.Count > 0;
    public bool HasSwapError => !string.IsNullOrEmpty(SwapErrorText);
    public bool CanDismissSwap => !SwapNeedsReload;
    public string SwapConfirmText => SwapNeedsReload ? "Reload exercise" : "Confirm swap";
    public bool ShowNoSwapCandidates => !IsSwapLoading && !HasSwapCandidates && !HasSwapError;
    public bool HasCelebration => Celebration != CelebrationType.None;
    public string ExercisePositionText => $"Exercise {CurrentExerciseIndex + 1} of {TotalExercisesInActivity}";
    public string TipsButtonText => IsTipsExpanded ? "Hide tips & technique" : "Tips & technique";
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
        ResetSwapPanel();
        ActivityCards = [];
        SelectedActivity = null;
        CurrentExercise = null;
        CurrentExerciseIndex = 0;
        TotalExercisesInActivity = 0;
        ExerciseProgress = [];
        ExercisePreviewNames = [];
        DayTitle = "Workout";
        SessionHistory = [];
        CyclePhaseNote = string.Empty;
        _skippedIds.Clear();
        _prIds.Clear();
        _savedLogs.Clear();
        _activityTimers.Clear();
        Celebration = CelebrationType.None;
        IsActivityComplete = false;
        CurrentScreen = SessionScreen.DayOverview;
        ErrorText = string.Empty;
        IsBusy = true;
        try
        {
            if (workoutDayId <= 0)
                throw new InvalidOperationException("This workout could not be found. Go back and choose a workout.");

            var detail = await _session.GetDetailAsync(workoutDayId);
            var existingSession = await _session.GetPlannedSessionAsync(workoutDayId);
            var history = await _session.GetPlannedSessionHistoryAsync(workoutDayId);
            if (version != _loadVersion) return;
            SessionHistory = new ObservableCollection<string>(history.Select(entry => string.Format(
                UiTextCatalog.Get(entry.CompletedAtUtc.HasValue
                    ? "Workout_Session_HistoryCompletedFormat" : "Workout_Session_HistoryActiveFormat"),
                entry.StartedAtUtc.ToLocalTime().ToString("g"),
                entry.CompletedAtUtc?.ToLocalTime().ToString("g") ?? string.Empty, entry.SessionId)));
            _isRestDay = detail.IsRestDay;
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
            string phaseNote;
            try
            {
                var overview = await _cycleOverview.LoadAsync(DateTime.Today, DateTime.Today);
                phaseNote = BuildPhaseNote(overview?.Prediction.CurrentPhase ?? CyclePhase.Unknown);
            }
            catch
            {
                phaseNote = "Cycle guidance is unavailable. Follow your energy and comfort today.";
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
            if (!string.IsNullOrWhiteSpace(activityTag))
            {
                var activity = ActivityCards.FirstOrDefault(item =>
                    string.Equals(item.Tag, activityTag, StringComparison.OrdinalIgnoreCase));
                if (activity is not null)
                    SelectActivity(activity);
            }
        }
        catch (Exception)
        {
            if (version == _loadVersion)
                ErrorText = "Could not load this workout. Try again.";
        }
        finally
        {
            if (version == _loadVersion) IsBusy = false;
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
        if (IsBusy || !IsDayOverview || activity is null || !ActivityCards.Contains(activity)) return;
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
        ActivityIntroIntensity = rpe.Success ? $"Target RPE {rpe.Groups[1].Value}" :
            targets.Contains("steady", StringComparison.OrdinalIgnoreCase) ? "Steady effort" :
            "Follow the targets at a comfortable effort";
        ActivityPrescriptionText = FormatPrescription(SelectedActivity.Prescription);
    }

    private static string FormatPrescription(ActivitySessionPrescriptionOutputV020? prescription)
    {
        if (prescription is null) return string.Empty;
        var lines = new List<string>();
        void AddValue(string key, decimal? value)
        {
            if (value is not null)
                lines.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, UiTextCatalog.Get(key), value));
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
            lines.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                UiTextCatalog.Get("Workout_Session_PrescriptionWorkIntervals"),
                prescription.IntervalWorkCount, prescription.IntervalWorkSeconds));
        if (prescription.IntervalRecoveryCount is not null && prescription.IntervalRecoverySeconds is not null)
            lines.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                UiTextCatalog.Get("Workout_Session_PrescriptionRecoveryIntervals"),
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
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
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
        if (!SelectedActivity.HasExercises)
        {
            IsTimedActivityRunning = true;
            return;
        }
        CurrentScreen = SessionScreen.ExerciseFlow;
        SetCurrentExercise(0);
    }

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
                CelebrationType.BigPR => $"{exercise.Name}: new weight best!",
                CelebrationType.MediumFirstTime => $"First {exercise.Name} log. Great start!",
                _ => $"{exercise.Name} logged"
            };
        }
        catch (Exception)
        {
            if (version == _loadVersion)
                ErrorText = "Could not save this exercise. Your entries are still here. Try again.";
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
            ? "<1 min"
            : $"{(int)timer.Elapsed.TotalMinutes} min";
        var logged = activity.Exercises.Count(exercise => exercise.IsLogged);
        IsActivityComplete = logged == ids.Count;
        SummaryTitle = IsActivityComplete ? "Activity complete!" : "Activity reviewed";
        SummaryStatusText = $"{logged} of {ids.Count} logged" +
                            (ids.Any(_skippedIds.Contains)
                                ? $" · {ids.Count(_skippedIds.Contains)} skipped"
                                : string.Empty);
        ErrorText = string.Empty;
        RefreshProgress();
        CurrentScreen = SessionScreen.ActivitySummary;
    }

    [RelayCommand]
    private async Task FinishActivityAsync()
    {
        if (IsBusy || SwapNeedsReload) return;
        if (!await CompletePlannedSessionAsync()) return;
        PauseActivity();
        CloseSwapPanel();
        RefreshActivityCards();
        SelectedActivity = null;
        CurrentExercise = null;
        Celebration = CelebrationType.None;
        ErrorText = string.Empty;
        CurrentScreen = SessionScreen.DayOverview;
    }

    [RelayCommand]
    private async Task FinishTimedActivityAsync()
    {
        if (!IsTimedActivityRunning || !await CompletePlannedSessionAsync()) return;
        IsTimedActivityRunning = false;
        await _navigateAsync("..");
    }

    [RelayCommand]
    private async Task CompleteSessionAsync()
    {
        if (await CompletePlannedSessionAsync())
            await _navigateAsync("..");
    }

    private async Task<bool> CompletePlannedSessionAsync()
    {
        if (!await _completionGate.WaitAsync(NoCompletionWaitMilliseconds)) return false;
        try { return await CompletePlannedSessionCoreAsync(); }
        finally { _completionGate.Release(); }
    }

    private async Task<bool> CompletePlannedSessionCoreAsync()
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
            ErrorText = "Complete at least one second of activity before finishing.";
            _pendingCompletionSeconds = null;
            IsTimedActivityRunning = false;
            CurrentScreen = SessionScreen.ActivityIntro;
            return false;
        }
        _pendingCompletionAtUtc ??= DateTime.UtcNow;
        OnPropertyChanged(nameof(CanAbandonPlannedSession));
        IsBusy = true;
        try
        {
            await _session.FinishPlannedSessionAsync(sessionId, _pendingCompletionAtUtc.Value, _pendingCompletionSeconds.Value);
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
    private async Task AbandonPlannedSessionAsync()
    {
        if (_pendingCompletionAtUtc is not null)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
            return;
        }
        if (IsBusy || _plannedSessionId is not { } sessionId) return;
        IsBusy = true;
        try
        {
            await _session.AbandonPlannedSessionAsync(sessionId);
            _timingLoopCancellation?.Cancel();
            _plannedSessionId = null;
            OnPropertyChanged(nameof(CanAbandonPlannedSession));
            _plannedSessionTimer.Reset();
            _sessionCreated = false;
            _timingIntervalId = null;
            _pendingTimingCheckpoint = null;
            _pendingResumeOperationId = null;
            _pendingCompletionAtUtc = null;
            _pendingCompletionSeconds = null;
            _confirmedActiveSeconds = 0;
            _plannedSessionStartedAtUtc = default;
            IsTimedActivityRunning = false;
            await _navigateAsync("..");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ErrorText = "Workout_Session_ConnectionRetry";
        }
        catch (Exception)
        {
            ErrorText = "Workout_Session_AbandonFailed";
        }
        finally { IsBusy = false; }
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

        if (IsDayOverview)
        {
            await FinishSessionAsync();
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
        SelectedActivity = null;
        CurrentExercise = null;
        CurrentScreen = SessionScreen.DayOverview;
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
        var wasActive = IsExerciseFlow || IsTimedActivityRunning;
        var suspend = SuspendTimingAsync();
        if (wasActive && _pendingCompletionAtUtc is null)
        {
            IsTimedActivityRunning = false;
            CurrentScreen = SessionScreen.ActivityIntro;
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
            if (version == _swapVersion) SwapErrorText = "Could not load alternatives. Close this panel and try again.";
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
                SwapErrorText = "The exercise may have changed. Reload it before continuing, or leave this session.";
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
                MetricText = $"{count} {(count == 1 ? "exercise" : "exercises")}",
                SummaryText = $"{activity.Exercises.Count(exercise => exercise.IsLogged)} of {count} logged" +
                              (skipped > 0 ? $" · {skipped} skipped" : string.Empty),
                ActivityBackground = activity.ActivityBackground,
                Prescription = activity.Prescription,
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
        return "Cycle phase is context. Use your current check-in and restrictions when reviewing your plan.";
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
            ErrorText = "Add positive sets and reps before logging this exercise.";
        else if (exercise.UsesStrengthMetrics && exercise.UsesWeight &&
                 ParseOptionalFloat(exercise.LoggedWeightText) is null)
            ErrorText = "Add a valid weight in kg. Use 0 kg for bodyweight.";
        else if (exercise.UsesDurationMetric && ParseDurationSeconds(exercise.LoggedDurationMinutesText) is null)
            ErrorText = "Add a positive duration in minutes before logging this exercise.";
        else if ((exercise.UsesDistanceMetric && InvalidOptionalFloat(exercise.LoggedDistanceKmText)) ||
                 (exercise.UsesHeartRateMetric && InvalidOptionalInt(exercise.LoggedHeartRateText)) ||
                 (exercise.UsesPowerMetric && InvalidOptionalInt(exercise.LoggedPowerWattsText)) ||
                 (exercise.UsesCadenceMetric && InvalidOptionalInt(exercise.LoggedCadenceRpmText)) ||
                 (exercise.UsesPaceMetric && !string.IsNullOrWhiteSpace(exercise.LoggedPaceText) &&
                  ParsePace(exercise.LoggedPaceText) is null) ||
                 (exercise.UsesEffortMetric && !string.IsNullOrWhiteSpace(exercise.LoggedEffortText) &&
                  ParsePositiveInt(exercise.LoggedEffortText) is not (>= 1 and <= 10)))
            ErrorText =
                "Check your optional metrics. Use positive numbers, pace as minutes:seconds, and effort from 1 to 10.";
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
        return new WorkoutExerciseLogDto(
            item.WorkoutDayExerciseId, item.ExerciseId,
            item.UsesStrengthMetrics ? ParsePositiveInt(item.LoggedSetsText) : 0,
            item.UsesStrengthMetrics ? ParsePositiveInt(item.LoggedRepsText) : 0,
            item.UsesStrengthMetrics ? item.UsesWeight ? ParseOptionalFloat(item.LoggedWeightText) : 0f : null,
            item.UsesDurationMetric ? ParseDurationSeconds(item.LoggedDurationMinutesText) : null,
            item.UsesDistanceMetric ? ParseOptionalFloat(item.LoggedDistanceKmText) : null,
            item.UsesHeartRateMetric ? ParseOptionalInt(item.LoggedHeartRateText) : null,
            item.UsesPaceMetric ? ParsePace(item.LoggedPaceText) : null,
            item.UsesPowerMetric ? ParseOptionalInt(item.LoggedPowerWattsText) : null,
            item.UsesCadenceMetric ? ParseOptionalInt(item.LoggedCadenceRpmText) : null,
            item.UsesEffortMetric && ParsePositiveInt(item.LoggedEffortText) is >= 1 and <= 10 and var effort
                ? effort
                : null);
    }
}
