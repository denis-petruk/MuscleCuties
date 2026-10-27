using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.ViewModels.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Workout.Archive;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class StrengthPlanGenerationViewModel : ObservableObject, IPageLoadAware
{
    private const int NoPolicyReasons = 0;
    private const long InitialAnswersVersion = 0;
    private const string NoStatusMessage = "";
    private const int RecentWorkoutArchiveLimit = 100;
    private const int DaysPerHistoryPeriod = 7;
    private const int RecentHistoryPeriodCount = 4;
    private const int RecentHistoryWindowDays = DaysPerHistoryPeriod * RecentHistoryPeriodCount;
    private const int DefaultSessionWarmupMinutes = 5;
    private const int DefaultSessionCooldownMinutes = 5;
    private const int LeaseRenewalIntervalMinutes = 2;
    private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromMinutes(LeaseRenewalIntervalMinutes);

    private const string PlanChangedKey = "Workout_Generation_PlanChanged";
    private const string ConnectionErrorKey = "Workout_Generation_ConnectionError";
    private const string RequiredAnswersKey = "Workout_Generation_RequiredAnswers";
    private const string AcceptUnavailableKey = "Workout_Generation_AcceptUnavailable";
    private const string AcceptFailedKey = "Workout_Generation_AcceptFailed";
    private const string AppliedTrainNavigationFailedKey = "Workout_Generation_AppliedTrainNavigationFailed";
    private const string StrengthPreferenceRequiredKey = "Workout_Generation_StrengthPreferenceRequired";
    private const string AvailableDaysMissingKey = "Workout_Generation_AvailableDaysMissing";
    private const string LoadFailedKey = "Workout_Generation_LoadFailed";
    private const string CreateFailedKey = "Workout_Generation_CreateFailed";
    private const string OptionalActivitiesSkippedKey = "Workout_Generation_OptionalActivitiesSkipped";
    private const string PreviewNotSavedKey = "Workout_Generation_PreviewNotSaved";
    private const string ReplacementTitleKey = "Workout_Generation_ReplacementTitle";
    private const string ReplacementSummaryKey = "Workout_Generation_ReplacementSummary";
    private const string ReplacementCancelKey = "Workout_Generation_ReplacementCancel";
    private const string ApplyPlanKey = "Workout_Generation_ApplyPlan";
    private const string CommonCancelKey = "Common_Cancel";
    private const string ReducedVolumeNoticeKey = "Workout_Generation_ReducedVolumeNotice";
    private const string ReducedVolumeTitleKey = "Workout_Generation_ReducedVolumeTitle";
    private const string ExploreOtherFeaturesKey = "Workout_Generation_ExploreOtherFeatures";
    private const string NoSavedWorkoutHistoryKey = "Workout_Generation_NoSavedWorkoutHistory";
    private const string RecentHistorySummaryFormatKey = "Workout_Generation_RecentHistorySummaryFormat";
    private const string HistoryUnavailableKey = "Workout_Generation_HistoryUnavailable";
    private const string HideTrainingDetailsKey = "Workout_Generation_HideTrainingDetails";
    private const string EditTrainingDetailsKey = "Workout_Generation_EditTrainingDetails";
    private const string EvidenceYesKey = "Workout_Generation_EvidenceYes";
    private const string EvidenceNoKey = "Workout_Generation_EvidenceNo";
    private const string SwimmingContinuousKey = "Workout_Generation_SwimmingContinuous";
    private const string SwimmingIntervalsKey = "Workout_Generation_SwimmingIntervals";
    private const string YogaGentleKey = "Workout_Generation_YogaGentle";
    private const string YogaDemandingKey = "Workout_Generation_YogaDemanding";
    private const string SavedAvailabilityFormatKey = "Workout_Generation_SavedAvailabilityFormat";
    private const string RecentWeekLastSevenDaysKey = "Workout_Generation_RecentWeekLastSevenDays";
    private const string RecentWeekRangeFormatKey = "Workout_Generation_RecentWeekRangeFormat";
    private const string RecentWeekAccessibilityFormatKey = "Workout_Generation_RecentWeekAccessibilityFormat";
    private const int HistoryStartOffset = 0;
    private const int HistoryOffsetOne = 1;

    private static readonly WorkoutActivityDto[] StrengthActivities = WorkoutPlanningConstants.StrengthActivities;
    private static readonly DayOfWeek[] CalendarWeekdays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private readonly IStrengthPlanProposalClientV020 _proposalClient;
    private readonly IProfileWorkoutPreferencesUseCase _preferences;
    private readonly IWorkoutArchiveUseCase _workoutArchive;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task> _navigateToTrainAsync;
    private readonly INavigationContextService? _navigationContext;
    private readonly Func<string, Task<bool>>? _confirmReplacementAsync;
    private readonly Func<Task>? _navigateToDashboardAsync;
    private readonly IWorkoutAccountCalendarClient? _accountCalendar;
    private readonly IWorkoutOverviewUseCase? _overview;
    private readonly Func<Task<bool>>? _confirmReducedVolumeAsync;
    private WorkoutPreferencesDto? _draftPreferences;
    private SavedActivityAnswersDto? _savedAnswers;
    private bool _safetyPlanChanged;
    private HashSet<WorkoutActivityDto> _selectedProfileActivities = [];
    private DayOfWeek[] _availableWeekdays = [];
    private WorkoutActivityDto? _historyActivity;
    private StrengthPlanProposalCreatedV020? _proposal;
    private bool _proposalInvalidated;
    private bool _showMissingAnswers;
    private bool _hasExistingPlan;
    private AcceptStrengthPlanProposalRequestV020? _pendingAcceptance;
    private Guid? _activeOtherProposalId;
    private CancellationTokenSource? _leaseRenewalCancellation;
    private bool _historyWasSelfReported;

    [ObservableProperty] private ObservableCollection<StrengthPlanRecentWeekDisplay> _recentTrainingWeeks = [];
    [ObservableProperty] private ObservableCollection<StrengthPlanSessionDisplay> _strengthPreviewSessions = [];
    [ObservableProperty] private ObservableCollection<MixedActivitySessionDisplay> _mixedActivitySessions = [];
    [ObservableProperty] private ObservableCollection<MixedActivityDayDisplay> _mixedActivityDays = [];
    [ObservableProperty] private ObservableCollection<MixedActivityDecisionDisplay> _activityDecisions = [];
    [ObservableProperty] private ObservableCollection<string> _scheduleInputReasons = [];
    [ObservableProperty] private ObservableCollection<string> _previewLimitations = [];
    [ObservableProperty] private StrengthPlanActivityChoice? _selectedActivity;
    [ObservableProperty] private ObservableCollection<StrengthPlanActivityChoice> _fittingStrengthStyleChoices = [];
    [ObservableProperty] private StrengthPlanActivityChoice? _selectedFittingStrengthStyle;
    [ObservableProperty] private ObservableCollection<StrengthPlanActivityChoice> _hiitUnderlyingCardioActivities = [];
    [ObservableProperty] private StrengthPlanActivityChoice? _selectedHiitUnderlyingCardioActivity;
    [ObservableProperty] private HiitEligibilityEvidenceCaptureViewModel _hiitEvidenceCapture = new();
    [ObservableProperty] private StrengthPlanPreviewResponseV020? _preview;
    [ObservableProperty] private string _consistentWeeks = string.Empty;
    [ObservableProperty] private string _averageSessionsPerWeek = string.Empty;
    [ObservableProperty] private string _daysSinceLastSession = string.Empty;
    [ObservableProperty] private string _warmupMinutes = DefaultSessionWarmupMinutes.ToString(CultureInfo.InvariantCulture);
    [ObservableProperty] private string _cooldownMinutes = DefaultSessionCooldownMinutes.ToString(CultureInfo.InvariantCulture);
    [ObservableProperty] private ObservableCollection<ActivityEvidenceAnswerChoice> _evidenceYesNoChoices = [];
    [ObservableProperty] private ObservableCollection<ActivityEvidenceAnswerChoice> _swimmingStyleChoices = [];
    [ObservableProperty] private ObservableCollection<ActivityEvidenceAnswerChoice> _yogaContentChoices = [];
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedSwimmingFacilityAccessChoice;
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedIndependentSwimmingChoice;
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedSwimmingStyleChoice;
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedRopeClimbingCompetenceChoice;
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedClimbingInstructionChoice;
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedYogaContentChoice;
    [ObservableProperty] private bool? _hasSwimmingFacilityAccess;
    [ObservableProperty] private bool? _canSwimIndependently;
    [ObservableProperty] private bool? _wantsContinuousSwimming;
    [ObservableProperty] private bool? _hasRopeClimbingCompetence;
    [ObservableProperty] private bool? _appropriateClimbingInstructionAvailable;
    [ObservableProperty] private bool? _isDemandingYogaContent;
    [ObservableProperty] private string _statusMessage = NoStatusMessage;
    [ObservableProperty] private string _recentTrainingSummary = string.Empty;
    [ObservableProperty] private bool _isTrainingDetailsExpanded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isLoadingData;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isProposalAccepted;
    [ObservableProperty] private bool _isTrainNavigationPending;

    public StrengthPlanGenerationViewModel(
        IStrengthPlanProposalClientV020 proposalClient,
        IProfileWorkoutPreferencesUseCase preferences,
        IWorkoutArchiveUseCase workoutArchive,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateBackAsync,
        Func<Task> navigateToTrainAsync,
        INavigationContextService? navigationContext = null,
        Func<string, Task<bool>>? confirmReplacementAsync = null,
        Func<Task>? navigateToDashboardAsync = null,
        IWorkoutAccountCalendarClient? accountCalendar = null,
        Func<Task<bool>>? confirmReducedVolumeAsync = null,
        IWorkoutOverviewUseCase? overview = null)
    {
        _proposalClient = proposalClient;
        _preferences = preferences;
        _workoutArchive = workoutArchive;
        _refreshNotifier = refreshNotifier;
        _navigateBackAsync = navigateBackAsync;
        _navigateToTrainAsync = navigateToTrainAsync;
        _navigationContext = navigationContext;
        _confirmReplacementAsync = confirmReplacementAsync;
        _navigateToDashboardAsync = navigateToDashboardAsync;
        _accountCalendar = accountCalendar;
        _overview = overview;
        _confirmReducedVolumeAsync = confirmReducedVolumeAsync ?? (async () =>
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlertAsync(
                    UiTextCatalog.Get(ReducedVolumeTitleKey) is var title && !string.Equals(title, ReducedVolumeTitleKey, StringComparison.Ordinal)
                        ? title
                        : "Reduced workout volume",
                    UiTextCatalog.Get(ReducedVolumeNoticeKey) is var notice && !string.Equals(notice, ReducedVolumeNoticeKey, StringComparison.Ordinal)
                        ? notice
                        : "This plan uses reduced workout volume to fit your selected duration and schedule.",
                    UiTextCatalog.Get(ApplyPlanKey),
                    UiTextCatalog.Get(CommonCancelKey))));
        EvidenceYesNoChoices = CreateYesNoChoices();
        SwimmingStyleChoices = CreateSwimmingStyleChoices();
        YogaContentChoices = CreateYogaContentChoices();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ToggleTrainingDetailsCommand = new RelayCommand(ToggleTrainingDetails, CanToggleTrainingDetails);
        CreateProposalCommand = new AsyncRelayCommand(CreateProposalAsync, CanCreateProposal);
        AcceptProposalCommand = new AsyncRelayCommand(AcceptProposalAsync, CanAcceptProposal);
        EditDetailsCommand = new AsyncRelayCommand(EditDetailsAsync, CanEditDetails);
        ClosePreviewCommand = new AsyncRelayCommand(ClosePreviewAsync, CanClosePreview);
        KeepCurrentPlanCommand = new AsyncRelayCommand(_navigateBackAsync);
        BackCommand = new AsyncRelayCommand(BackAsync, () => !IsBusy);
        OpenTrainCommand = new AsyncRelayCommand(OpenTrainAsync, CanOpenTrain);
        ExploreOtherFeaturesCommand = new AsyncRelayCommand(ExploreOtherFeaturesAsync, CanExploreOtherFeatures);
        TakeOverPreviewCommand = new AsyncRelayCommand(TakeOverPreviewAsync, () => !IsBusy && HasActiveOtherPreview);
    }

    public AsyncRelayCommand LoadCommand { get; }
    public RelayCommand ToggleTrainingDetailsCommand { get; }
    public AsyncRelayCommand CreateProposalCommand { get; }
    public AsyncRelayCommand AcceptProposalCommand { get; }
    public AsyncRelayCommand EditDetailsCommand { get; }
    public AsyncRelayCommand ClosePreviewCommand { get; }
    public AsyncRelayCommand KeepCurrentPlanCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand OpenTrainCommand { get; }
    public AsyncRelayCommand ExploreOtherFeaturesCommand { get; }
    public AsyncRelayCommand TakeOverPreviewCommand { get; }
    public AsyncRelayCommand LoadDataCommand => LoadCommand;

    public bool HasPreview => Preview is not null;
    public bool CanReviewPreview => HasPreview && !IsProposalAccepted && !IsTrainNavigationPending;
    public bool CanApplyPlanFromPreview => CanReviewPreview;
    public bool CanEditDetailsFromPreview => CanReviewPreview;
    public bool HasFeasiblePreview => CanSubmitProposal;
    public bool HasLimitation => Preview is not null && Preview.Status != "Feasible";
    public bool HasMixedSchedule => Preview?.MixedActivitySchedule is not null;
    public bool HasMixedActivitySessions => MixedActivitySessions.Count > NoPolicyReasons;
    public bool HasNoMixedActivitySessions => !HasMixedActivitySessions;
    public bool HasActivityDecisions => ActivityDecisions.Count > NoPolicyReasons;
    public bool HasOptionalOmissions => Preview?.MixedActivitySchedule?.Activities.Any(
        decision => decision.ActivityTypeId != (int)WorkoutActivityDto.RestDay &&
                    decision.ScheduledSessionsPerWeek == NoPolicyReasons &&
                    !decision.ReasonCodes.Contains("StrengthAlternativeNotChosen") &&
                    !decision.ReasonCodes.Contains("ReplacedWithGeneralStrength")) == true;
    public bool ExpandActivityDecisions => HasLimitation || HasOptionalOmissions;
    public bool HasScheduleInputReasons => ScheduleInputReasons.Count > NoPolicyReasons;
    public bool HasStrengthPreviewSessions => StrengthPreviewSessions.Count > NoPolicyReasons;
    public bool IsBeforePreview => Preview is null;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool IsHistorySelfReported => _historyWasSelfReported;
    public string TrainingDetailsToggleText => UiTextCatalog.Get(IsTrainingDetailsExpanded
        ? HideTrainingDetailsKey
        : EditTrainingDetailsKey);
    public bool ShouldShowHiitUnderlyingCardioChoice =>
        _selectedProfileActivities.Contains(WorkoutActivityDto.Hiit) &&
        HiitUnderlyingCardioActivities.Count > NoPolicyReasons;
    public bool IsSwimmingSelected => _selectedProfileActivities.Contains(WorkoutActivityDto.Swimming);
    public bool IsRockClimbingSelected => _selectedProfileActivities.Contains(WorkoutActivityDto.RockClimbing);
    public bool IsYogaSelected => _selectedProfileActivities.Contains(WorkoutActivityDto.Yoga);
    public bool MissingSwimmingFacility => _showMissingAnswers && IsSwimmingSelected && HasSwimmingFacilityAccess is null;
    public bool MissingSwimmingAbility => _showMissingAnswers && IsSwimmingSelected && HasSwimmingFacilityAccess is true && CanSwimIndependently is null;
    public bool MissingSwimmingStyle => _showMissingAnswers && IsSwimmingSelected && CanChooseSwimmingStyle && WantsContinuousSwimming is null;
    public bool MissingClimbingCompetence => _showMissingAnswers && IsRockClimbingSelected && HasRopeClimbingCompetence is null;
    public bool MissingClimbingInstruction => _showMissingAnswers && ShouldAskClimbingInstruction && AppropriateClimbingInstructionAvailable is null;
    public bool MissingYogaType => _showMissingAnswers && IsYogaSelected && IsDemandingYogaContent is null;
    public bool CanOfferExploreOtherFeatures =>
        !HasExistingPlan && IsBeforePreview &&
        (IsSwimmingSelected || IsRockClimbingSelected || IsYogaSelected ||
         ShouldShowHiitUnderlyingCardioChoice || HasFittingStrengthStyleChoices);
    public bool HasExistingPlan => _hasExistingPlan || (_proposal?.ReplacesExistingPlan == true);
    public bool HasActiveOtherPreview => _activeOtherProposalId.HasValue;
    public bool HasFittingStrengthStyleChoices => FittingStrengthStyleChoices.Count > NoPolicyReasons;
    public bool CanKeepCurrentPlan => HasExistingPlan && IsBeforePreview;
    public bool RequiresReducedVolumeAcknowledgment =>
        Preview.RequiresReducedVolumeAcknowledgment();
    public string ReducedVolumeNoticeText
    {
        get
        {
            var text = UiTextCatalog.Get(ReducedVolumeNoticeKey);
            return !string.Equals(text, ReducedVolumeNoticeKey, StringComparison.Ordinal)
                ? text
                : "This plan uses reduced workout volume to fit your selected duration and schedule.";
        }
    }
    public string ExploreOtherFeaturesButtonText
    {
        get
        {
            var text = UiTextCatalog.Get(ExploreOtherFeaturesKey);
            return !string.Equals(text, ExploreOtherFeaturesKey, StringComparison.Ordinal)
                ? text
                : "Explore other features";
        }
    }
    public bool CanChooseSwimmingStyle => HasSwimmingFacilityAccess is true && CanSwimIndependently is true;
    public bool ShouldAskClimbingInstruction => IsRockClimbingSelected && HasRopeClimbingCompetence is true;
    public bool HasSavedAvailability => HasValidSavedAvailability;
    public bool HasSavedStrengthActivity => SelectedActivity is not null;
    public bool IsPageLoading => IsLoadingData;
    public bool CanEditInputs => !IsBusy && Preview is null && !IsProposalAccepted && !IsTrainNavigationPending;
    public string SavedAvailabilitySummary => StrengthPlanGenerationDisplayMapper.FormatLocalized(
        SavedAvailabilityFormatKey,
        string.Join(", ", _availableWeekdays.Select(StrengthPlanGenerationDisplayMapper.FullWeekdayLabel)));
    public string PreviewLimitationMessage =>
        StrengthPlanGenerationDisplayMapper.GetPreviewLimitationMessage(Preview);
    public bool CanSubmitProposal => !_proposalInvalidated && !IsProposalAccepted && !IsTrainNavigationPending &&
        _proposal is { IsPersisted: true, Lease: not null } &&
        Preview?.Status == "Feasible";

    private bool HasValidSavedAvailability =>
        _availableWeekdays.Length is >= WorkoutPreferencesConstraints.MinimumAvailableWeekdays and
            <= WorkoutPreferencesConstraints.MaximumAvailableWeekdays;

    private bool HasMissingRequiredAnswers()
    {
        if (IsSwimmingSelected)
        {
            if (HasSwimmingFacilityAccess is null)
                return true;
            if (HasSwimmingFacilityAccess is true && CanSwimIndependently is null)
                return true;
            if (CanChooseSwimmingStyle && WantsContinuousSwimming is null)
                return true;
        }

        if (IsRockClimbingSelected)
        {
            if (HasRopeClimbingCompetence is null)
                return true;
            if (ShouldAskClimbingInstruction && AppropriateClimbingInstructionAvailable is null)
                return true;
        }

        if (IsYogaSelected && IsDemandingYogaContent is null)
            return true;

        return false;
    }

    private async Task InitializePlanPreviewOrAutoApplyAsync()
    {
        var selectedActivity = SelectedActivity;
        if (selectedActivity is null || !HasValidSavedAvailability)
            return;

        if (HasMissingRequiredAnswers())
            return;

        if (!StrengthPlanGenerationInputReader.TryRead(
                selectedActivity.Activity,
                ConsistentWeeks,
                AverageSessionsPerWeek,
                DaysSinceLastSession,
                WarmupMinutes,
                CooldownMinutes,
                IsHistorySelfReported,
                DateOnly.FromDateTime(DateTime.Now),
                out var history,
                out var warmup,
                out var cooldown,
                out _))
        {
            return;
        }

        var preferences = _draftPreferences ?? await _preferences.LoadAsync();
        if (preferences is null || !preferences.ActivityTypes.Contains((int)selectedActivity.Activity))
            return;

        _availableWeekdays = NormalizeWeekdays(preferences.AvailableWeekdays);
        UpdateSelectedProfileActivities(preferences.ActivityTypes);
        NotifyAvailabilityChanged();

        var request = BuildPreviewRequest(selectedActivity.Activity, preferences, history, warmup, cooldown);

        _proposal = await _proposalClient.CreateAsync(request);
        if (HandlePreflightRequirement(_proposal.Preview))
        {
            _proposal = null;
            return;
        }
        _hasExistingPlan = _proposal.ReplacesExistingPlan;
        _proposalInvalidated = false;
        StartLeaseRenewal(_proposal);
        PopulatePreviewDisplays(_proposal.Preview);

        if (WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(_proposal.ReplacesExistingPlan,
                _proposal.Preview.Status == "Feasible", _proposal.IsPersisted, HasMissingRequiredAnswers(),
                _proposal.Preview.StrengthStyleChoiceRequired, HasOptionalOmissions, RequiresReducedVolumeAcknowledgment))
        {
            IsBusy = false;
            await AcceptProposalAsync();
        }
    }

    private async Task LoadAsync()
    {
        IsLoadError = false;
        IsLoadingData = true;
        if (!IsTrainNavigationPending)
        {
            IsProposalAccepted = false;
            if (Preview is not null)
                await ClosePreviewAsync();
        }
        IsBusy = true;
        StatusMessage = NoStatusMessage;
        try
        {
            if (_accountCalendar is not null)
                await _accountCalendar.UpdateForegroundTimeZoneAsync();
            if (_overview is not null)
                _hasExistingPlan = (await _overview.LoadAsync(new WorkoutOverviewRequest(DateOnly.FromDateTime(DateTime.Now)))).PlanName is not null;
            OnPropertyChanged(nameof(HasExistingPlan));
            OnPropertyChanged(nameof(CanOfferExploreOtherFeatures));
            _activeOtherProposalId = (await _proposalClient.GetActivePreviewAsync()).ProposalId;
            OnPropertyChanged(nameof(HasActiveOtherPreview));
            if (_navigationContext?.TryTake<WorkoutPreferencesDto>(ProfileWorkoutPreferencesViewModel.DraftPreferencesContextKey, out var draft) == true)
                _draftPreferences = draft;
            var preferences = _draftPreferences ?? await _preferences.LoadAsync();
            preferences ??= new WorkoutPreferencesDto(
                ActivityTypes: [],
                AvailableWeekdays: [],
                ActivitySessionDurationsMinutes: new Dictionary<int, int>());

            var weekdays = NormalizeWeekdays(preferences.AvailableWeekdays);
            _availableWeekdays = weekdays;
            var initialActivities = preferences.ActivityTypes.ToHashSet();
            UpdateSelectedProfileActivities(initialActivities.ToList());
            _savedAnswers = await _proposalClient.ReadAnswersAsync();
            PrefillActivityAnswers(_savedAnswers);
            NotifyAvailabilityChanged();
            var savedStrengthActivity = preferences.ActivityTypes
                .Select(activity => (WorkoutActivityDto)activity)
                .Where(StrengthActivities.Contains)
                .FirstOrDefault();
            SelectedActivity = StrengthActivities.Contains(savedStrengthActivity) &&
                               preferences.ActivityTypes.Contains((int)savedStrengthActivity)
                ? new StrengthPlanActivityChoice(savedStrengthActivity,
                    StrengthPlanGenerationDisplayMapper.ActivityLabel(savedStrengthActivity),
                    StrengthPlanGenerationDisplayMapper.StrengthPlanDescription(savedStrengthActivity))
                : null;

            if (string.IsNullOrWhiteSpace(WarmupMinutes))
                WarmupMinutes = DefaultSessionWarmupMinutes.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(CooldownMinutes))
                CooldownMinutes = DefaultSessionCooldownMinutes.ToString(CultureInfo.InvariantCulture);

            await LoadRecentWorkoutHistoryAsync(DateOnly.FromDateTime(DateTime.Now));

            if (SelectedActivity is null)
                StatusMessage = UiTextCatalog.Get(StrengthPreferenceRequiredKey);
            else if (!HasValidSavedAvailability)
                StatusMessage = UiTextCatalog.Get(AvailableDaysMissingKey);
            else if (!HasActiveOtherPreview && !HasMissingRequiredAnswers())
            {
                await InitializePlanPreviewOrAutoApplyAsync();
            }

            CreateProposalCommand.NotifyCanExecuteChanged();
        }
        catch (HttpRequestException)
        {
            IsLoadError = true;
            StatusMessage = UiTextCatalog.Get(ConnectionErrorKey);
        }
        catch (TaskCanceledException)
        {
            IsLoadError = true;
            StatusMessage = UiTextCatalog.Get(ConnectionErrorKey);
        }
        catch (Exception)
        {
            IsLoadError = true;
            StatusMessage = UiTextCatalog.Get(LoadFailedKey);
        }
        finally
        {
            IsBusy = false;
            IsLoadingData = false;
            CreateProposalCommand.NotifyCanExecuteChanged();
            NotifyCommandStates();
        }
    }

    private async Task LoadRecentWorkoutHistoryAsync(DateOnly localDate)
    {
        try
        {
            var workoutHistory = await _workoutArchive.GetWorkoutHistoryAsync(RecentWorkoutArchiveLimit, HistoryStartOffset);
            var sessionDates = workoutHistory
                .Select(session => ToLocalSessionDate(session.StartedAt))
                .Where(sessionDate => sessionDate <= localDate)
                .OrderByDescending(sessionDate => sessionDate)
                .ToArray();

            RecentTrainingWeeks = BuildRecentTrainingWeeks(sessionDates, localDate);
            if (sessionDates.Length == NoPolicyReasons)
            {
                RecentTrainingSummary = UiTextCatalog.Get(NoSavedWorkoutHistoryKey);
                return;
            }

            var recentWindowStart = localDate.AddDays(-(RecentHistoryWindowDays - HistoryOffsetOne));
            var recentSessionCount = sessionDates.Count(sessionDate => sessionDate >= recentWindowStart);
            RecentTrainingSummary = StrengthPlanGenerationDisplayMapper.FormatLocalized(
                RecentHistorySummaryFormatKey,
                recentSessionCount.ToString(CultureInfo.CurrentCulture));

            // Archive summaries have no activity identifier, so they cannot establish
            // resistance-specific history for GS/HVS eligibility.
        }
        catch (Exception)
        {
            RecentTrainingWeeks = BuildRecentTrainingWeeks([], localDate);
            RecentTrainingSummary = UiTextCatalog.Get(HistoryUnavailableKey);
        }
    }

    private static ObservableCollection<StrengthPlanRecentWeekDisplay> BuildRecentTrainingWeeks(
        DateOnly[] sessionDates,
        DateOnly localDate)
    {
        var result = new ObservableCollection<StrengthPlanRecentWeekDisplay>();
        for (var periodIndex = 0; periodIndex < RecentHistoryPeriodCount; periodIndex++)
        {
            var daysAgo = periodIndex * DaysPerHistoryPeriod;
            var periodEnd = localDate.AddDays(-daysAgo);
            var periodStart = periodEnd.AddDays(-(DaysPerHistoryPeriod - HistoryOffsetOne));
            var count = sessionDates.Count(sessionDate => sessionDate >= periodStart && sessionDate <= periodEnd);
            var periodLabel = periodIndex == NoPolicyReasons
                ? UiTextCatalog.Get(RecentWeekLastSevenDaysKey)
                : StrengthPlanGenerationDisplayMapper.FormatLocalized(
                    RecentWeekRangeFormatKey,
                    (daysAgo + HistoryOffsetOne).ToString(CultureInfo.CurrentCulture),
                    (daysAgo + DaysPerHistoryPeriod).ToString(CultureInfo.CurrentCulture));
            result.Add(new StrengthPlanRecentWeekDisplay(
                periodLabel,
                count.ToString(CultureInfo.CurrentCulture),
                StrengthPlanGenerationDisplayMapper.FormatLocalized(
                    RecentWeekAccessibilityFormatKey,
                    periodLabel,
                    count.ToString(CultureInfo.CurrentCulture))));
        }

        return result;
    }

    private static DateOnly ToLocalSessionDate(DateTime startedAt) =>
        DateOnly.FromDateTime(DateTime.SpecifyKind(startedAt, DateTimeKind.Utc).ToLocalTime());

    private bool CanCreateProposal() => !IsBusy && !IsLoadError && !HasActiveOtherPreview && SelectedActivity is not null &&
        HasValidSavedAvailability && Preview is null && !IsProposalAccepted && !IsTrainNavigationPending;

    private bool CanToggleTrainingDetails() => CanEditInputs;

    private void ToggleTrainingDetails() => IsTrainingDetailsExpanded = !IsTrainingDetailsExpanded;

    private Task CreateProposalAsync() => CreatePreviewAsync();

    private async Task CreatePreviewAsync()
    {
        StatusMessage = NoStatusMessage;
        var selectedActivity = SelectedActivity;
        if (selectedActivity is null)
            return;

        if (!StrengthPlanGenerationInputReader.TryRead(
                selectedActivity.Activity,
                ConsistentWeeks,
                AverageSessionsPerWeek,
                DaysSinceLastSession,
                WarmupMinutes,
                CooldownMinutes,
                IsHistorySelfReported,
                DateOnly.FromDateTime(DateTime.Now),
                out var history,
                out var warmup,
                out var cooldown,
                out var inputErrorMessageKey))
        {
            StatusMessage = UiTextCatalog.Get(inputErrorMessageKey);
            return;
        }

        IsBusy = true;
        try
        {
            var preferences = _draftPreferences ?? await _preferences.LoadAsync();
            if (preferences is null)
            {
                StatusMessage = UiTextCatalog.Get(StrengthPreferenceRequiredKey);
                return;
            }

            _availableWeekdays = NormalizeWeekdays(preferences.AvailableWeekdays);
            UpdateSelectedProfileActivities(preferences.ActivityTypes);
            NotifyAvailabilityChanged();
            if (!preferences.ActivityTypes.Contains((int)selectedActivity.Activity))
            {
                SelectedActivity = null;
                StatusMessage = UiTextCatalog.Get(StrengthPreferenceRequiredKey);
                return;
            }
            if (!HasValidSavedAvailability)
            {
                StatusMessage = UiTextCatalog.Get(AvailableDaysMissingKey);
                return;
            }

            var request = BuildPreviewRequest(selectedActivity.Activity, preferences, history, warmup, cooldown);

            _showMissingAnswers = true;
            NotifyMissingAnswers();
            var currentEvidence = request.ActivityEligibilityEvidence ?? [];
            var savedEvidence = (_savedAnswers?.ActivityEligibilityEvidence ?? [])
                .Where(answer => currentEvidence.All(current => current.Activity != answer.Activity))
                .Concat(currentEvidence).ToArray();
            _savedAnswers = await _proposalClient.SaveAnswersAsync(new SaveActivityAnswersRequest(
                _savedAnswers?.Version ?? InitialAnswersVersion, savedEvidence,
                request.HiitEligibilityEvidence ?? _savedAnswers?.HiitEligibilityEvidence));
            _safetyPlanChanged |= _savedAnswers.SafetyPlanChanged;
            if (_safetyPlanChanged)
            {
                _refreshNotifier.InvalidateWorkoutAndDashboard();
                StatusMessage = UiTextCatalog.Get("Workout_Generation_SafetyPlanChanged");
            }

            if (MissingSwimmingFacility || MissingSwimmingAbility || MissingSwimmingStyle ||
                MissingClimbingCompetence || MissingClimbingInstruction || MissingYogaType)
            {
                StatusMessage = RequiredAnswersMessage();
                return;
            }

            _proposal = await _proposalClient.CreateAsync(request);
            if (HandlePreflightRequirement(_proposal.Preview))
            {
                _proposal = null;
                return;
            }
            _hasExistingPlan = _proposal.ReplacesExistingPlan;
            _proposalInvalidated = false;
            StartLeaseRenewal(_proposal);
            PopulatePreviewDisplays(_proposal.Preview);
            if (_safetyPlanChanged)
                StatusMessage = UiTextCatalog.Get("Workout_Generation_SafetyPlanChanged");
        }
        catch (HttpRequestException)
        {
            StatusMessage = WithSafetyNotice(UiTextCatalog.Get(ConnectionErrorKey));
        }
        catch (TaskCanceledException)
        {
            StatusMessage = WithSafetyNotice(UiTextCatalog.Get(ConnectionErrorKey));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StrengthPlanGeneration] Proposal creation failed: {ex.GetType().Name}.");
            StatusMessage = WithSafetyNotice(UiTextCatalog.Get(CreateFailedKey));
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private void PopulatePreviewDisplays(StrengthPlanPreviewResponseV020 preview)
    {
        Preview = preview;
        StrengthPreviewSessions = new ObservableCollection<StrengthPlanSessionDisplay>(
            preview.Sessions.Select(StrengthPlanSessionDisplay.From));
        var mixedSchedule = preview.MixedActivitySchedule;
        MixedActivitySessions = new ObservableCollection<MixedActivitySessionDisplay>(
            mixedSchedule?.Sessions.Select(StrengthPlanGenerationDisplayMapper.ToMixedActivitySessionDisplay) ??
            Array.Empty<MixedActivitySessionDisplay>());
        MixedActivityDays = new ObservableCollection<MixedActivityDayDisplay>(
                mixedSchedule?.Sessions.Count > NoPolicyReasons ? CalendarWeekdays.Select(day =>
                {
                    var sessions = mixedSchedule.Sessions.Where(session => session.Weekday == day)
                        .Select(session => StrengthPlanGenerationDisplayMapper.ToMixedActivitySessionDisplay(session))
                        .ToArray();
                    return new MixedActivityDayDisplay(
                        StrengthPlanGenerationDisplayMapper.FullWeekdayLabel(day),
                        sessions.Length == NoPolicyReasons
                            ? UiTextCatalog.Get("Workout_Generation_PlannedRestDay")
                            : string.Join(" · ", sessions.Select(session => session.Activity + " " + session.Duration)),
                        sessions.Length == NoPolicyReasons);
                }) : []);
            ActivityDecisions = new ObservableCollection<MixedActivityDecisionDisplay>(
                mixedSchedule?.Activities
                    .Where(decision => decision.ActivityTypeId != (int)WorkoutActivityDto.RestDay)
                    .Select(StrengthPlanGenerationDisplayMapper.ToMixedActivityDecisionDisplay) ??
                Array.Empty<MixedActivityDecisionDisplay>());
            ScheduleInputReasons = new ObservableCollection<string>(
                mixedSchedule?.InputReasonCodes
                    .Where(code => !StrengthPlanGenerationDisplayMapper.IsInformationalDiagnosticCode(code))
                    .Select(StrengthPlanGenerationDisplayMapper.ScheduleReasonMessage)
                    .Distinct() ??
                Array.Empty<string>());
            PreviewLimitations = new ObservableCollection<string>(HasLimitation
                ? [PreviewLimitationMessage]
                : []);
            StatusMessage = preview.Status == "Feasible" && (_proposal?.IsPersisted ?? false)
            ? HasOptionalOmissions
                ? UiTextCatalog.Get(OptionalActivitiesSkippedKey)
                : NoStatusMessage
            : preview.Status == "Feasible"
                ? UiTextCatalog.Get(PreviewNotSavedKey)
                : NoStatusMessage;

        NotifyPreviewChanged();
    }

    private StrengthPlanPreviewRequestV020 BuildPreviewRequest(
        WorkoutActivityDto activity,
        WorkoutPreferencesDto preferences,
        ActivityTrainingHistoryInputV020? history,
        int warmupMinutes,
        int cooldownMinutes)
    {
        var localDate = DateOnly.FromDateTime(DateTime.Now);
        var underlyingCardioActivity = ShouldShowHiitUnderlyingCardioChoice
            ? SelectedHiitUnderlyingCardioActivity?.Activity
            : null;

        return new StrengthPlanPreviewRequestV020(activity,
            localDate,
            history,
            WarmupMinutes: warmupMinutes,
            CooldownMinutes: cooldownMinutes)
        {
            DraftPreferences = preferences,
            ExplicitStrengthStyleChoice = SelectedFittingStrengthStyle?.Activity,
            SelectedActivityTypes = preferences.ActivityTypes.ToHashSet(),
            AvailableWeekdays = _availableWeekdays.ToHashSet(),
            ActivitySessionDurationsMinutes = preferences.ActivitySessionDurationsMinutes,
            RequestedSessionsPerWeek = preferences.RequestedSessionsPerWeek,
            ExcludedExerciseIds = preferences.ExcludedExerciseIds?.ToHashSet(),
            ActivityEligibilityEvidence = StrengthPlanActivityEvidenceMapper.Create(
                _selectedProfileActivities,
                HasSwimmingFacilityAccess,
                CanSwimIndependently,
                WantsContinuousSwimming,
                HasRopeClimbingCompetence,
                AppropriateClimbingInstructionAvailable,
                IsDemandingYogaContent),
            HiitUnderlyingCardioActivity = underlyingCardioActivity,
            HiitEligibilityEvidence = underlyingCardioActivity is { } selectedUnderlyingActivity
                ? HiitEvidenceCapture.ToRequestInput(localDate, selectedUnderlyingActivity)
                : null
        };
    }

    private bool CanAcceptProposal() => WorkoutPlanningFlowRule.CanAccept(IsBusy, IsProposalAccepted,
        IsTrainNavigationPending, _proposalInvalidated, _proposal?.IsPersisted == true,
        _proposal?.Lease is not null, Preview?.Status == "Feasible", _pendingAcceptance is not null);

    private bool CanExploreOtherFeatures() => !IsBusy && CanOfferExploreOtherFeatures;

    private async Task ExploreOtherFeaturesAsync()
    {
        if (!CanExploreOtherFeatures())
            return;

        if (_navigateToDashboardAsync is not null)
            await _navigateToDashboardAsync();
    }

    private async Task TakeOverPreviewAsync()
    {
        if (_activeOtherProposalId is not { } proposalId)
            return;
        IsBusy = true;
        try
        {
            var result = await _proposalClient.TakeoverAsync(proposalId);
            if (result.Lease is not { } lease || !string.Equals(result.Status, "Active", StringComparison.Ordinal))
            {
                StatusMessage = UiTextCatalog.Get(PlanChangedKey);
                return;
            }
            var closed = await _proposalClient.CloseAsync(new PlanningLeaseRequest(proposalId, lease.LeaseId, lease.Fence));
            if (!string.Equals(closed.Status, "Closed", StringComparison.Ordinal))
            {
                StatusMessage = UiTextCatalog.Get(PlanChangedKey);
                return;
            }
            _activeOtherProposalId = null;
            OnPropertyChanged(nameof(HasActiveOtherPreview));
            StatusMessage = NoStatusMessage;
        }
        catch (HttpRequestException)
        {
            StatusMessage = UiTextCatalog.Get(ConnectionErrorKey);
        }
        catch (Exception)
        {
            StatusMessage = UiTextCatalog.Get(CreateFailedKey);
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private bool HandlePreflightRequirement(StrengthPlanPreviewResponseV020 preview)
    {
        if (string.Equals(preview.Status, "RequiredAnswersMissing", StringComparison.Ordinal))
        {
            _showMissingAnswers = true;
            NotifyMissingAnswers();
            StatusMessage = RequiredAnswersMessage();
            return true;
        }
        if (!preview.StrengthStyleChoiceRequired)
            return false;
        FittingStrengthStyleChoices = new ObservableCollection<StrengthPlanActivityChoice>(
            preview.FittingStrengthStyleIds.Select(id => (WorkoutActivityDto)id)
                .Select(activity => new StrengthPlanActivityChoice(activity,
                    StrengthPlanGenerationDisplayMapper.ActivityLabel(activity))));
        SelectedFittingStrengthStyle = null;
        OnPropertyChanged(nameof(HasFittingStrengthStyleChoices));
        OnPropertyChanged(nameof(CanOfferExploreOtherFeatures));
        ExploreOtherFeaturesCommand.NotifyCanExecuteChanged();
        StatusMessage = _safetyPlanChanged
            ? $"{UiTextCatalog.Get("Workout_Generation_SafetyPlanChanged")}{Environment.NewLine}{UiTextCatalog.Get("Workout_Generation_ChooseFittingStrengthStyle")}"
            : UiTextCatalog.Get("Workout_Generation_ChooseFittingStrengthStyle");
        return true;
    }

    private bool CanEditDetails() => !IsBusy && Preview is not null && !IsProposalAccepted;

    private bool CanClosePreview() => !IsBusy && Preview is not null;

    private bool CanOpenTrain() => !IsBusy && IsTrainNavigationPending;

    private async Task BackAsync()
    {
        if (IsBusy)
            return;

        var action = WorkoutPlanningFlowRule.Back(Preview is not null, IsTrainNavigationPending);
        if (action == WorkoutPlanningBackAction.ClosePreview)
        {
            await ClosePreviewAsync();
            return;
        }

        if (action == WorkoutPlanningBackAction.ResumeAfterApply)
        {
            await OpenTrainAsync();
            return;
        }

        await _navigateBackAsync();
    }

    private async Task EditDetailsAsync()
    {
        await ClosePreviewAsync();
        if (_draftPreferences is not null)
            _navigationContext?.Set(ProfileWorkoutPreferencesViewModel.DraftPreferencesContextKey, _draftPreferences);
        try
        {
            await _navigateBackAsync();
        }
        catch (Exception)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(ProfileWorkoutPreferencesPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StrengthPlanGeneration] Fallback navigation to Preferences failed: {ex.GetType().Name}.");
            }
        }
    }

    private async Task ClosePreviewAsync()
    {
        _leaseRenewalCancellation?.Cancel();
        if (_proposal?.Lease is { } lease)
        {
            try
            {
                await _proposalClient.CloseAsync(new PlanningLeaseRequest(_proposal.ProposalId, lease.LeaseId, lease.Fence));
            }
            catch (Exception)
            {
                // The server lease expires if the connection is unavailable.
            }
        }
        ClearPreview(clearStatus: !IsTrainNavigationPending);
    }

    private async Task OpenTrainAsync()
    {
        if (!CanOpenTrain())
            return;

        IsBusy = true;
        try
        {
            if (WorkoutPlanningFlowRule.Success(_proposal?.ReplacesExistingPlan != false) == WorkoutPlanningSuccessDestination.Dashboard && _navigateToDashboardAsync is not null)
                await _navigateToDashboardAsync();
            else
                await _navigateToTrainAsync();
            IsTrainNavigationPending = false;
            StatusMessage = NoStatusMessage;
            NotifyPreviewChanged();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[StrengthPlanGeneration] Train navigation retry failed: {exception.GetType().Name}.");
            StatusMessage = UiTextCatalog.Get(AppliedTrainNavigationFailedKey);
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task AcceptProposalAsync()
    {
        if (_proposal is null || !CanAcceptProposal())
        {
            StatusMessage = UiTextCatalog.Get(AcceptUnavailableKey);
            return;
        }

        IsBusy = true;
        StatusMessage = NoStatusMessage;
        try
        {
            if (_proposal.Lease is not { } lease)
            {
                _proposalInvalidated = true;
                StatusMessage = UiTextCatalog.Get(PlanChangedKey);
                NotifyPreviewChanged();
                NotifyCommandStates();
                return;
            }
            if (_pendingAcceptance is null)
            {
                if (RequiresReducedVolumeAcknowledgment)
                {
                    if (_confirmReducedVolumeAsync is null || !await _confirmReducedVolumeAsync())
                        return;
                }

                if (_proposal.ReplacesExistingPlan)
                {
                    if (_confirmReplacementAsync is null || !await _confirmReplacementAsync(BuildReplacementSummary()))
                        return;
                }

                var renewed = await _proposalClient.RenewAsync(new PlanningLeaseRequest(
                    _proposal.ProposalId, lease.LeaseId, lease.Fence));
                if (renewed.Lease is null || string.Equals(renewed.Status, "Stale", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(renewed.Status, "Expired", StringComparison.OrdinalIgnoreCase))
                {
                    _proposalInvalidated = true;
                    StatusMessage = UiTextCatalog.Get(PlanChangedKey);
                    NotifyPreviewChanged();
                    NotifyCommandStates();
                    return;
                }
                lease = renewed.Lease;
                _pendingAcceptance = new AcceptStrengthPlanProposalRequestV020(
                    _proposal.ProposalId, lease.LeaseId, lease.Fence, _proposal.ProposalVersion,
                    ReplacementConfirmed: _proposal.ReplacesExistingPlan,
                    ReducedVolumeAcknowledged: RequiresReducedVolumeAcknowledgment);
            }
            var result = await _proposalClient.AcceptAsync(_pendingAcceptance);
            if (result.Status != "Accepted")
            {
                _pendingAcceptance = null;
                var statusKey = result.Status is "Conflict" or "Stale"
                    ? PlanChangedKey
                    : AcceptFailedKey;
                if (result.Status is "Conflict" or "Stale")
                {
                    _proposalInvalidated = true;
                    NotifyPreviewChanged();
                }
                StatusMessage = UiTextCatalog.Get(statusKey);
                return;
            }

            _refreshNotifier.InvalidateWorkoutAndDashboard();
            _leaseRenewalCancellation?.Cancel();
            IsProposalAccepted = true;
            IsTrainNavigationPending = true;
            NotifyPreviewChanged();
            try
            {
                if (WorkoutPlanningFlowRule.Success(_proposal.ReplacesExistingPlan) == WorkoutPlanningSuccessDestination.Dashboard && _navigateToDashboardAsync is not null)
                    await _navigateToDashboardAsync();
                else
                    await _navigateToTrainAsync();
                IsTrainNavigationPending = false;
                StatusMessage = NoStatusMessage;
                NotifyPreviewChanged();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[StrengthPlanGeneration] Accepted plan could not open Train ({exception.GetType().Name}).");
                StatusMessage = UiTextCatalog.Get(AppliedTrainNavigationFailedKey);
            }
        }
        catch (HttpRequestException)
        {
            StatusMessage = UiTextCatalog.Get(ConnectionErrorKey);
        }
        catch (TaskCanceledException)
        {
            StatusMessage = UiTextCatalog.Get(ConnectionErrorKey);
        }
        catch (Exception)
        {
            StatusMessage = UiTextCatalog.Get(AcceptFailedKey);
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    partial void OnSelectedActivityChanged(StrengthPlanActivityChoice? value)
    {
        OnPropertyChanged(nameof(HasSavedStrengthActivity));
        if (_historyActivity == value?.Activity)
            return;

        _historyActivity = value?.Activity;
        ConsistentWeeks = string.Empty;
        AverageSessionsPerWeek = string.Empty;
        DaysSinceLastSession = string.Empty;
        WarmupMinutes = DefaultSessionWarmupMinutes.ToString(CultureInfo.InvariantCulture);
        CooldownMinutes = DefaultSessionCooldownMinutes.ToString(CultureInfo.InvariantCulture);
        _historyWasSelfReported = false;
        OnPropertyChanged(nameof(IsHistorySelfReported));
        ClearPreview();
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsTrainingDetailsExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(TrainingDetailsToggleText));

    partial void OnConsistentWeeksChanged(string value) => MarkHistoryAsSelfReported();

    partial void OnAverageSessionsPerWeekChanged(string value) => MarkHistoryAsSelfReported();

    partial void OnDaysSinceLastSessionChanged(string value) => MarkHistoryAsSelfReported();

    private void MarkHistoryAsSelfReported()
    {
        if (_historyWasSelfReported)
            return;

        _historyWasSelfReported = true;
        OnPropertyChanged(nameof(IsHistorySelfReported));
    }

    private void ClearPreview(bool clearStatus = true)
    {
        _leaseRenewalCancellation?.Cancel();
        _leaseRenewalCancellation = null;
        _pendingAcceptance = null;
        _proposalInvalidated = false;
        _proposal = null;
        Preview = null;
        FittingStrengthStyleChoices.Clear();
        SelectedFittingStrengthStyle = null;
        OnPropertyChanged(nameof(HasFittingStrengthStyleChoices));
        StrengthPreviewSessions.Clear();
        MixedActivitySessions.Clear();
        MixedActivityDays.Clear();
        ActivityDecisions.Clear();
        ScheduleInputReasons.Clear();
        PreviewLimitations.Clear();
        if (clearStatus)
            StatusMessage = NoStatusMessage;

        NotifyPreviewChanged();
    }

    private void StartLeaseRenewal(StrengthPlanProposalCreatedV020 proposal)
    {
        _leaseRenewalCancellation?.Cancel();
        if (proposal.Lease is null)
            return;
        var cancellation = new CancellationTokenSource();
        _leaseRenewalCancellation = cancellation;
        _ = RenewLeaseUntilClosedAsync(proposal, cancellation.Token);
    }

    private string BuildReplacementSummary()
    {
        var activities = Preview?.MixedActivitySchedule?.Activities
            .Where(decision => decision.ActivityTypeId != (int)WorkoutActivityDto.RestDay)
            .Select(StrengthPlanGenerationDisplayMapper.ToMixedActivityDecisionDisplay)
            .Select(decision => $"{decision.Activity}: {decision.Status}. {decision.Frequency} {string.Join(", ", decision.Reasons)}".Trim())
            .ToArray() ?? [];
        return string.Join(Environment.NewLine, activities.Prepend(UiTextCatalog.Get(ReplacementSummaryKey)));
    }

    private string RequiredAnswersMessage() => WithSafetyNotice(UiTextCatalog.Get(RequiredAnswersKey));

    private string WithSafetyNotice(string message) => _safetyPlanChanged
        ? $"{UiTextCatalog.Get("Workout_Generation_SafetyPlanChanged")}{Environment.NewLine}{message}"
        : message;

    private void PrefillActivityAnswers(SavedActivityAnswersDto saved)
    {
        var swimming = saved.ActivityEligibilityEvidence.FirstOrDefault(answer => answer.Activity == WorkoutActivityDto.Swimming);
        var climbing = saved.ActivityEligibilityEvidence.FirstOrDefault(answer => answer.Activity == WorkoutActivityDto.RockClimbing);
        var yoga = saved.ActivityEligibilityEvidence.FirstOrDefault(answer => answer.Activity == WorkoutActivityDto.Yoga);
        SelectedSwimmingFacilityAccessChoice = EvidenceYesNoChoices.FirstOrDefault(choice => choice.Value == swimming?.HasSwimmingFacilityAccess);
        SelectedIndependentSwimmingChoice = EvidenceYesNoChoices.FirstOrDefault(choice => choice.Value == swimming?.CanSwimIndependently);
        SelectedSwimmingStyleChoice = SwimmingStyleChoices.FirstOrDefault(choice => choice.Value == swimming?.WantsContinuousSwimming);
        SelectedRopeClimbingCompetenceChoice = EvidenceYesNoChoices.FirstOrDefault(choice => choice.Value == climbing?.HasRopeClimbingCompetence);
        SelectedClimbingInstructionChoice = EvidenceYesNoChoices.FirstOrDefault(choice => choice.Value == climbing?.AppropriateClimbingInstructionAvailable);
        SelectedYogaContentChoice = YogaContentChoices.FirstOrDefault(choice => choice.Value == yoga?.IsDemandingYogaContent);
    }

    private void NotifyMissingAnswers()
    {
        OnPropertyChanged(nameof(MissingSwimmingFacility));
        OnPropertyChanged(nameof(MissingSwimmingAbility));
        OnPropertyChanged(nameof(MissingSwimmingStyle));
        OnPropertyChanged(nameof(MissingClimbingCompetence));
        OnPropertyChanged(nameof(MissingClimbingInstruction));
        OnPropertyChanged(nameof(MissingYogaType));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(HasSwimmingFacilityAccess) or nameof(CanSwimIndependently) or
            nameof(WantsContinuousSwimming) or nameof(HasRopeClimbingCompetence) or
            nameof(AppropriateClimbingInstructionAvailable) or nameof(IsDemandingYogaContent))
            NotifyMissingAnswers();
    }

    private async Task RenewLeaseUntilClosedAsync(StrengthPlanProposalCreatedV020 proposal, CancellationToken cancellationToken)
    {
        try
        {
            var lease = proposal.Lease!;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(LeaseRenewalInterval, cancellationToken);
                if (DateTimeOffset.UtcNow >= lease.ExpiresAtUtc)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!ReferenceEquals(_proposal, proposal) || cancellationToken.IsCancellationRequested ||
                            _pendingAcceptance is not null)
                            return;
                        _proposalInvalidated = true;
                        StatusMessage = UiTextCatalog.Get(PlanChangedKey);
                        NotifyPreviewChanged();
                        NotifyCommandStates();
                    });
                    return;
                }

                PlanningLeaseResult result;
                try
                {
                    result = await _proposalClient.RenewAsync(
                        new PlanningLeaseRequest(proposal.ProposalId, lease.LeaseId, lease.Fence), cancellationToken);
                }
                catch (HttpRequestException)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!ReferenceEquals(_proposal, proposal) || cancellationToken.IsCancellationRequested)
                            return;
                        StatusMessage = UiTextCatalog.Get(ConnectionErrorKey);
                    });
                    continue;
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!ReferenceEquals(_proposal, proposal) || cancellationToken.IsCancellationRequested)
                            return;
                        StatusMessage = UiTextCatalog.Get(ConnectionErrorKey);
                    });
                    continue;
                }

                if (result.Lease is null || string.Equals(result.Status, "Stale", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(result.Status, "Expired", StringComparison.OrdinalIgnoreCase))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!ReferenceEquals(_proposal, proposal) || cancellationToken.IsCancellationRequested ||
                            _pendingAcceptance is not null)
                            return;
                        _proposalInvalidated = true;
                        StatusMessage = UiTextCatalog.Get(PlanChangedKey);
                        NotifyPreviewChanged();
                        NotifyCommandStates();
                    });
                    return;
                }
                lease = result.Lease;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            // Transient error; next loop or Apply revalidates
        }
    }

    private void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(IsBeforePreview));
        OnPropertyChanged(nameof(CanReviewPreview));
        OnPropertyChanged(nameof(CanApplyPlanFromPreview));
        OnPropertyChanged(nameof(CanEditDetailsFromPreview));
        OnPropertyChanged(nameof(HasFeasiblePreview));
        OnPropertyChanged(nameof(HasLimitation));
        OnPropertyChanged(nameof(HasMixedSchedule));
        OnPropertyChanged(nameof(HasMixedActivitySessions));
        OnPropertyChanged(nameof(HasNoMixedActivitySessions));
        OnPropertyChanged(nameof(HasActivityDecisions));
        OnPropertyChanged(nameof(HasOptionalOmissions));
        OnPropertyChanged(nameof(ExpandActivityDecisions));
        OnPropertyChanged(nameof(HasScheduleInputReasons));
        OnPropertyChanged(nameof(HasStrengthPreviewSessions));
        OnPropertyChanged(nameof(CanEditInputs));
        OnPropertyChanged(nameof(CanSubmitProposal));
        OnPropertyChanged(nameof(PreviewLimitationMessage));
        OnPropertyChanged(nameof(RequiresReducedVolumeAcknowledgment));
        OnPropertyChanged(nameof(ReducedVolumeNoticeText));
        OnPropertyChanged(nameof(CanOfferExploreOtherFeatures));
        OnPropertyChanged(nameof(CanKeepCurrentPlan));
        OnPropertyChanged(nameof(HasExistingPlan));
        NotifyCommandStates();
    }

    private void NotifyAvailabilityChanged()
    {
        OnPropertyChanged(nameof(HasSavedAvailability));
        OnPropertyChanged(nameof(SavedAvailabilitySummary));
    }

    private void UpdateSelectedProfileActivities(IReadOnlyList<int>? activityTypes)
    {
        var previousUnderlyingActivity = SelectedHiitUnderlyingCardioActivity?.Activity;
        _selectedProfileActivities = activityTypes is null
            ? []
            : activityTypes.Where(value => Enum.IsDefined(typeof(WorkoutActivityDto), value))
                .Select(value => (WorkoutActivityDto)value)
                .ToHashSet();
        if (!IsSwimmingSelected)
        {
            SelectedSwimmingFacilityAccessChoice = null;
            SelectedIndependentSwimmingChoice = null;
            SelectedSwimmingStyleChoice = null;
        }
        if (!IsRockClimbingSelected)
        {
            SelectedRopeClimbingCompetenceChoice = null;
            SelectedClimbingInstructionChoice = null;
        }
        if (!IsYogaSelected)
            SelectedYogaContentChoice = null;
        HiitUnderlyingCardioActivities = new ObservableCollection<StrengthPlanActivityChoice>(
            WorkoutPlanningConstants.SupportedHiitUnderlyingCardioActivities
                .Where(_selectedProfileActivities.Contains)
                .Select(activity => new StrengthPlanActivityChoice(activity,
                    StrengthPlanGenerationDisplayMapper.ActivityLabel(activity))));
        SelectedHiitUnderlyingCardioActivity = HiitUnderlyingCardioActivities
            .FirstOrDefault(choice => choice.Activity == previousUnderlyingActivity);

        OnPropertyChanged(nameof(ShouldShowHiitUnderlyingCardioChoice));
        OnPropertyChanged(nameof(IsSwimmingSelected));
        OnPropertyChanged(nameof(IsRockClimbingSelected));
        OnPropertyChanged(nameof(IsYogaSelected));
        OnPropertyChanged(nameof(ShouldAskClimbingInstruction));
    }

    private static ObservableCollection<ActivityEvidenceAnswerChoice> CreateYesNoChoices() =>
    [
        new(UiTextCatalog.Get(EvidenceYesKey), true),
        new(UiTextCatalog.Get(EvidenceNoKey), false)
    ];

    private static ObservableCollection<ActivityEvidenceAnswerChoice> CreateSwimmingStyleChoices() =>
    [
        new(UiTextCatalog.Get(SwimmingContinuousKey), true),
        new(UiTextCatalog.Get(SwimmingIntervalsKey), false)
    ];

    private static ObservableCollection<ActivityEvidenceAnswerChoice> CreateYogaContentChoices() =>
    [
        new(UiTextCatalog.Get(YogaGentleKey), false),
        new(UiTextCatalog.Get(YogaDemandingKey), true)
    ];

    partial void OnSelectedSwimmingFacilityAccessChoiceChanged(ActivityEvidenceAnswerChoice? value) =>
        HasSwimmingFacilityAccess = value?.Value;

    partial void OnSelectedIndependentSwimmingChoiceChanged(ActivityEvidenceAnswerChoice? value) =>
        CanSwimIndependently = value?.Value;

    partial void OnSelectedSwimmingStyleChoiceChanged(ActivityEvidenceAnswerChoice? value) =>
        WantsContinuousSwimming = value?.Value;

    partial void OnSelectedRopeClimbingCompetenceChoiceChanged(ActivityEvidenceAnswerChoice? value) =>
        HasRopeClimbingCompetence = value?.Value;

    partial void OnSelectedClimbingInstructionChoiceChanged(ActivityEvidenceAnswerChoice? value) =>
        AppropriateClimbingInstructionAvailable = value?.Value;

    partial void OnSelectedYogaContentChoiceChanged(ActivityEvidenceAnswerChoice? value) =>
        IsDemandingYogaContent = value?.Value;

    partial void OnPreviewChanged(StrengthPlanPreviewResponseV020? value)
    {
        OnPropertyChanged(nameof(CanEditInputs));
    }

    partial void OnSelectedHiitUnderlyingCardioActivityChanged(
        StrengthPlanActivityChoice? value) => HiitEvidenceCapture.Reset();

    partial void OnHasSwimmingFacilityAccessChanged(bool? value)
    {
        OnPropertyChanged(nameof(CanChooseSwimmingStyle));
        if (value is not true)
            SelectedSwimmingStyleChoice = null;
    }

    partial void OnCanSwimIndependentlyChanged(bool? value)
    {
        OnPropertyChanged(nameof(CanChooseSwimmingStyle));
        if (value is not true)
            SelectedSwimmingStyleChoice = null;
    }

    partial void OnHasRopeClimbingCompetenceChanged(bool? value)
    {
        OnPropertyChanged(nameof(ShouldAskClimbingInstruction));
        if (value is false)
            SelectedClimbingInstructionChoice = null;
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditInputs));
        NotifyCommandStates();
    }

    partial void OnIsLoadingDataChanged(bool value) => OnPropertyChanged(nameof(IsPageLoading));

    private void NotifyCommandStates()
    {
        ToggleTrainingDetailsCommand.NotifyCanExecuteChanged();
        CreateProposalCommand.NotifyCanExecuteChanged();
        AcceptProposalCommand.NotifyCanExecuteChanged();
        EditDetailsCommand.NotifyCanExecuteChanged();
        ClosePreviewCommand.NotifyCanExecuteChanged();
        KeepCurrentPlanCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        OpenTrainCommand.NotifyCanExecuteChanged();
        ExploreOtherFeaturesCommand.NotifyCanExecuteChanged();
        TakeOverPreviewCommand.NotifyCanExecuteChanged();
    }

    private static DayOfWeek[] NormalizeWeekdays(IReadOnlyList<int>? values) =>
        values is null
            ? []
            : values.Where(value => Enum.IsDefined(typeof(DayOfWeek), value))
                .Select(value => (DayOfWeek)value)
                .Distinct()
                .ToArray();
}

public static class StrengthPlanPreviewExtensions
{
    private static readonly HashSet<string> ReducedVolumeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReducedVolume",
        "VolumeReduction",
        "HighVolumeConservativeStart"
    };

    public static bool RequiresReducedVolumeAcknowledgment(this StrengthPlanPreviewResponseV020? preview)
    {
        if (preview is null)
            return false;

        if (preview.RequiresReducedVolumeAcknowledgment)
            return true;

        if (preview.AdvisoryCodes.Any(code => ReducedVolumeCodes.Contains(code)))
            return true;

        if (preview.LimitationCodes.Any(code => ReducedVolumeCodes.Contains(code)))
            return true;

        if (preview.MixedActivitySchedule?.InputReasonCodes.Any(code => ReducedVolumeCodes.Contains(code)) == true)
            return true;

        return preview.MixedActivitySchedule?.Activities.Any(
            decision => decision.ReasonCodes.Any(code => ReducedVolumeCodes.Contains(code))) == true;
    }
}

