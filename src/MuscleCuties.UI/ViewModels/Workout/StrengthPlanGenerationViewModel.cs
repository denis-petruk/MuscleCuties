using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class StrengthPlanGenerationViewModel : ObservableObject
{
    private const int NoPolicyReasons = 0;
    private const string NoStatusMessage = "";

    private static readonly WorkoutActivityDto[] StrengthActivities = WorkoutPlanningConstants.StrengthActivities;

    private readonly IStrengthPlanProposalClientV020 _proposalClient;
    private readonly IProfileWorkoutPreferencesUseCase _preferences;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task> _navigateToTrainAsync;
    private HashSet<WorkoutActivityDto> _selectedProfileActivities = [];
    private DayOfWeek[] _availableWeekdays = [];
    private WorkoutActivityDto? _historyActivity;
    private WorkoutActivityDto? _candidateReviewActivity;
    private HashSet<int> _candidateReviewExerciseIds = [];
    private StrengthPlanProposalCreatedV020? _proposal;

    [ObservableProperty] private ObservableCollection<StrengthPlanActivityChoice> _activities = [];
    [ObservableProperty] private ObservableCollection<StrengthPlanSessionDisplay> _strengthPreviewSessions = [];
    [ObservableProperty] private ObservableCollection<StrengthPlanHighIntensityCandidateDisplay> _highIntensityCandidates = [];
    [ObservableProperty] private ObservableCollection<MixedActivitySessionDisplay> _mixedActivitySessions = [];
    [ObservableProperty] private ObservableCollection<MixedActivityDecisionDisplay> _activityDecisions = [];
    [ObservableProperty] private ObservableCollection<string> _scheduleInputReasons = [];
    [ObservableProperty] private ObservableCollection<string> _previewLimitations = [];
    [ObservableProperty] private StrengthPlanActivityChoice? _selectedActivity;
    [ObservableProperty] private ObservableCollection<StrengthPlanActivityChoice> _hiitUnderlyingCardioActivities = [];
    [ObservableProperty] private StrengthPlanActivityChoice? _selectedHiitUnderlyingCardioActivity;
    [ObservableProperty] private HiitEligibilityEvidenceCaptureViewModel _hiitEvidenceCapture = new();
    [ObservableProperty] private StrengthPlanPreviewResponseV020? _preview;
    [ObservableProperty] private string _consistentWeeks = "24";
    [ObservableProperty] private string _averageSessionsPerWeek = "3";
    [ObservableProperty] private string _daysSinceLastSession = "2";
    [ObservableProperty] private string _warmupMinutes = "5";
    [ObservableProperty] private string _cooldownMinutes = "5";
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
    [ObservableProperty] private bool _isBusy;

    public StrengthPlanGenerationViewModel(
        IStrengthPlanProposalClientV020 proposalClient,
        IProfileWorkoutPreferencesUseCase preferences,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateBackAsync,
        Func<Task> navigateToTrainAsync)
    {
        _proposalClient = proposalClient;
        _preferences = preferences;
        _refreshNotifier = refreshNotifier;
        _navigateBackAsync = navigateBackAsync;
        _navigateToTrainAsync = navigateToTrainAsync;
        EvidenceYesNoChoices = CreateYesNoChoices();
        SwimmingStyleChoices = CreateSwimmingStyleChoices();
        YogaContentChoices = CreateYogaContentChoices();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CreateProposalCommand = new AsyncRelayCommand(CreateProposalAsync, CanCreateProposal);
        ConfirmHighIntensityExercisesCommand = new AsyncRelayCommand(ConfirmHighIntensityExercisesAsync,
            CanConfirmHighIntensityExercises);
        AcceptProposalCommand = new AsyncRelayCommand(AcceptProposalAsync, CanAcceptProposal);
        EditDetailsCommand = new RelayCommand(EditDetails, CanEditDetails);
        KeepCurrentPlanCommand = new AsyncRelayCommand(_navigateBackAsync);
        BackCommand = new AsyncRelayCommand(_navigateBackAsync);
    }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand CreateProposalCommand { get; }
    public AsyncRelayCommand ConfirmHighIntensityExercisesCommand { get; }
    public AsyncRelayCommand AcceptProposalCommand { get; }
    public RelayCommand EditDetailsCommand { get; }
    public AsyncRelayCommand KeepCurrentPlanCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    public bool HasPreview => Preview is not null;
    public bool HasFeasiblePreview => Preview?.Status == "Feasible" && _proposal is { IsPersisted: true };
    public bool HasLimitation => Preview is not null && Preview.Status != "Feasible";
    public bool HasMixedSchedule => Preview?.MixedActivitySchedule is not null;
    public bool HasMixedActivitySessions => MixedActivitySessions.Count > NoPolicyReasons;
    public bool HasNoMixedActivitySessions => !HasMixedActivitySessions;
    public bool HasActivityDecisions => ActivityDecisions.Count > NoPolicyReasons;
    public bool HasScheduleInputReasons => ScheduleInputReasons.Count > NoPolicyReasons;
    public bool HasStrengthPreviewSessions => StrengthPreviewSessions.Count > NoPolicyReasons;
    public bool IsBeforePreview => Preview is null;
    public bool HasHighIntensityCandidates => SelectedActivity?.Activity == WorkoutActivityDto.StrengthHighIntensity &&
        Preview?.Status == "Limited" && HighIntensityCandidates.Count > NoPolicyReasons;
    public bool AreHighIntensityCandidateAnswersComplete => HasHighIntensityCandidates &&
        HighIntensityCandidates.All(candidate => candidate.FamiliarityChoice is not null &&
                                                 candidate.ManageableLoadChoice is not null);
    public bool CanAnswerHighIntensityCandidates => !IsBusy;
    public bool CanSubmitHighIntensityConfirmations => !IsBusy && AreHighIntensityCandidateAnswersComplete;
    public bool ShouldShowHiitUnderlyingCardioChoice =>
        _selectedProfileActivities.Contains(WorkoutActivityDto.Hiit) &&
        HiitUnderlyingCardioActivities.Count > NoPolicyReasons;
    public bool IsSwimmingSelected => _selectedProfileActivities.Contains(WorkoutActivityDto.Swimming);
    public bool IsRockClimbingSelected => _selectedProfileActivities.Contains(WorkoutActivityDto.RockClimbing);
    public bool IsYogaSelected => _selectedProfileActivities.Contains(WorkoutActivityDto.Yoga);
    public bool IsHighIntensityStrengthSelected =>
        SelectedActivity?.Activity == WorkoutActivityDto.StrengthHighIntensity;
    public bool CanChooseSwimmingStyle => HasSwimmingFacilityAccess is true && CanSwimIndependently is true;
    public bool ShouldAskClimbingInstruction => IsRockClimbingSelected && HasRopeClimbingCompetence is not false;
    public bool HasSavedAvailability => HasValidSavedAvailability;
    public bool CanEditInputs => !IsBusy && Preview is null;
    public string SavedAvailabilitySummary => StrengthPlanGenerationDisplayMapper.FormatLocalized(
        "Workout_Generation_SavedAvailabilityFormat",
        string.Join(", ", _availableWeekdays.Select(StrengthPlanGenerationDisplayMapper.FullWeekdayLabel)));
    public string PreviewLimitationMessage =>
        StrengthPlanGenerationDisplayMapper.GetPreviewLimitationMessage(Preview);
    public bool CanSubmitProposal => _proposal is { IsPersisted: true } && Preview?.Status == "Feasible";

    private bool HasValidSavedAvailability =>
        _availableWeekdays.Length is >= WorkoutPreferencesConstraints.MinimumAvailableWeekdays and
            <= WorkoutPreferencesConstraints.MaximumAvailableWeekdays;

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = NoStatusMessage;
        try
        {
            var preferences = await _preferences.LoadAsync();
            if (preferences is null)
            {
                _availableWeekdays = [];
                UpdateSelectedProfileActivities([]);
                Activities.Clear();
                SelectedActivity = null;
                NotifyAvailabilityChanged();
                ClearPreview(clearStatus: false);
                StatusMessage = UiTextCatalog.Get("Workout_Generation_MissingPreferences");
                return;
            }

            var weekdays = NormalizeWeekdays(preferences.AvailableWeekdays);
            if (weekdays.Length < WorkoutPreferencesConstraints.MinimumAvailableWeekdays)
            {
                weekdays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
            }
            _availableWeekdays = weekdays;
            var initialActivities = preferences.ActivityTypes.ToHashSet();
            initialActivities.Add((int)WorkoutActivityDto.GeneralStrength);
            UpdateSelectedProfileActivities(initialActivities.ToList());
            NotifyAvailabilityChanged();
            var strengthList = preferences.ActivityTypes
                .Where(value => Enum.IsDefined(typeof(WorkoutActivityDto), value))
                .Select(value => (WorkoutActivityDto)value)
                .Where(StrengthActivities.Contains)
                .ToList();
            if (!strengthList.Contains(WorkoutActivityDto.GeneralStrength))
                strengthList.Add(WorkoutActivityDto.GeneralStrength);

            Activities = new ObservableCollection<StrengthPlanActivityChoice>(
                strengthList.Select(activity => new StrengthPlanActivityChoice(activity,
                    StrengthPlanGenerationDisplayMapper.ActivityLabel(activity))));

            SelectedActivity = Activities.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(ConsistentWeeks))
                ConsistentWeeks = "24";
            if (string.IsNullOrWhiteSpace(AverageSessionsPerWeek))
                AverageSessionsPerWeek = "3";
            if (string.IsNullOrWhiteSpace(DaysSinceLastSession))
                DaysSinceLastSession = "2";
            if (string.IsNullOrWhiteSpace(WarmupMinutes))
                WarmupMinutes = "5";
            if (string.IsNullOrWhiteSpace(CooldownMinutes))
                CooldownMinutes = "5";

            var yesChoice = EvidenceYesNoChoices.FirstOrDefault(choice => choice.Value is true);
            if (IsSwimmingSelected)
            {
                SelectedSwimmingFacilityAccessChoice ??= yesChoice;
                SelectedIndependentSwimmingChoice ??= yesChoice;
                SelectedSwimmingStyleChoice ??= SwimmingStyleChoices.FirstOrDefault();
            }
            if (IsRockClimbingSelected)
            {
                SelectedRopeClimbingCompetenceChoice ??= yesChoice;
                SelectedClimbingInstructionChoice ??= yesChoice;
            }
            if (IsYogaSelected)
            {
                SelectedYogaContentChoice ??= YogaContentChoices.FirstOrDefault();
            }

            if (Activities.Count == NoPolicyReasons)
                StatusMessage = UiTextCatalog.Get("Workout_Generation_StrengthPreferenceRequired");
            else if (!HasValidSavedAvailability)
                StatusMessage = UiTextCatalog.Get("Workout_Generation_AvailableDaysMissing");

            CreateProposalCommand.NotifyCanExecuteChanged();
        }
        catch (Exception)
        {
            StatusMessage = UiTextCatalog.Get("Workout_Generation_LoadFailed");
        }
        finally
        {
            IsBusy = false;
            CreateProposalCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCreateProposal() => !IsBusy && SelectedActivity is not null &&
        HasValidSavedAvailability && Preview is null;

    private bool CanConfirmHighIntensityExercises() => CanSubmitHighIntensityConfirmations;

    private Task CreateProposalAsync() => CreatePreviewAsync(null, null);

    private Task ConfirmHighIntensityExercisesAsync()
    {
        if (!CanSubmitHighIntensityConfirmations)
            return Task.CompletedTask;

        var familiarExerciseIds = HighIntensityCandidates
            .Where(candidate => candidate.FamiliarityChoice?.Value is true)
            .Select(candidate => candidate.ExerciseId)
            .ToHashSet();
        var manageableLoadExerciseIds = HighIntensityCandidates
            .Where(candidate => candidate.ManageableLoadChoice?.Value is true)
            .Select(candidate => candidate.ExerciseId)
            .ToHashSet();

        return CreatePreviewAsync(familiarExerciseIds, manageableLoadExerciseIds);
    }

    private async Task CreatePreviewAsync(IReadOnlySet<int>? familiarExerciseIds,
        IReadOnlySet<int>? manageableLoadExerciseIds)
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
            var preferences = await _preferences.LoadAsync();
            if (preferences is null)
            {
                StatusMessage = UiTextCatalog.Get("Workout_Generation_MissingPreferences");
                return;
            }

            var weekdays = NormalizeWeekdays(preferences.AvailableWeekdays);
            if (weekdays.Length < WorkoutPreferencesConstraints.MinimumAvailableWeekdays)
            {
                weekdays = _availableWeekdays.Length >= WorkoutPreferencesConstraints.MinimumAvailableWeekdays
                    ? _availableWeekdays
                    : [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
            }
            _availableWeekdays = weekdays;
            var activeTypes = preferences.ActivityTypes.ToHashSet();
            activeTypes.Add((int)selectedActivity.Activity);
            UpdateSelectedProfileActivities(activeTypes.ToList());
            NotifyAvailabilityChanged();
            if (!HasValidSavedAvailability)
            {
                StatusMessage = UiTextCatalog.Get("Workout_Generation_AvailableDaysMissing");
                return;
            }

            var request = BuildPreviewRequest(selectedActivity.Activity, history, warmup, cooldown,
                familiarExerciseIds, manageableLoadExerciseIds);

            _proposal = await _proposalClient.CreateAsync(request);
            var preview = _proposal.Preview;
            Preview = preview;
            UpdateHighIntensityCandidates(preview);
            StrengthPreviewSessions = new ObservableCollection<StrengthPlanSessionDisplay>(
                preview.Sessions.Select(StrengthPlanSessionDisplay.From));
            var mixedSchedule = preview.MixedActivitySchedule;
            MixedActivitySessions = new ObservableCollection<MixedActivitySessionDisplay>(
                mixedSchedule?.Sessions.Select(StrengthPlanGenerationDisplayMapper.ToMixedActivitySessionDisplay) ??
                Array.Empty<MixedActivitySessionDisplay>());
            ActivityDecisions = new ObservableCollection<MixedActivityDecisionDisplay>(
                mixedSchedule?.Activities.Select(StrengthPlanGenerationDisplayMapper.ToMixedActivityDecisionDisplay) ??
                Array.Empty<MixedActivityDecisionDisplay>());
            ScheduleInputReasons = new ObservableCollection<string>(
                mixedSchedule?.InputReasonCodes.Select(StrengthPlanGenerationDisplayMapper.ScheduleReasonMessage) ??
                Array.Empty<string>());
            PreviewLimitations = new ObservableCollection<string>(HasLimitation
                ? [PreviewLimitationMessage]
                : []);
            StatusMessage = preview.Status == "Feasible" && _proposal.IsPersisted
                ? UiTextCatalog.Get("Workout_Generation_PreviewReady")
                : UiTextCatalog.Get("Workout_Generation_PreviewNotSaved");

            NotifyPreviewChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StrengthPlanGeneration] Proposal creation failed: {ex}");
            StatusMessage = UiTextCatalog.Get("Workout_Generation_CreateFailed");
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private StrengthPlanPreviewRequestV020 BuildPreviewRequest(
        WorkoutActivityDto activity,
        ActivityTrainingHistoryInputV020 history,
        int warmupMinutes,
        int cooldownMinutes,
        IReadOnlySet<int>? familiarExerciseIds,
        IReadOnlySet<int>? manageableLoadExerciseIds)
    {
        var localDate = DateOnly.FromDateTime(DateTime.Now);
        var underlyingCardioActivity = ShouldShowHiitUnderlyingCardioChoice
            ? SelectedHiitUnderlyingCardioActivity?.Activity
            : null;

        return new StrengthPlanPreviewRequestV020(activity,
            localDate,
            history,
            UserConfirmedFamiliarExerciseIds: familiarExerciseIds?.ToHashSet(),
            UserConfirmedManageableLoadExerciseIds: manageableLoadExerciseIds?.ToHashSet(),
            WarmupMinutes: warmupMinutes,
            CooldownMinutes: cooldownMinutes)
        {
            AvailableWeekdays = _availableWeekdays.ToHashSet(),
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

    private void UpdateHighIntensityCandidates(StrengthPlanPreviewResponseV020 preview)
    {
        var candidateSlots = preview.Sessions
            .Where(_ => preview.Status == "Limited" &&
                        SelectedActivity?.Activity == WorkoutActivityDto.StrengthHighIntensity)
            .SelectMany(session => session.Slots
                .Where(slot => slot.IsHeavyMain && slot.ExerciseId.HasValue)
                .Select(slot => new { Session = session, Slot = slot }))
            .ToArray();
        var candidateExerciseIds = candidateSlots
            .Select(candidate => candidate.Slot.ExerciseId!.Value)
            .ToHashSet();
        var canPreserveAnswers = _candidateReviewActivity == SelectedActivity?.Activity &&
                                 _candidateReviewExerciseIds.SetEquals(candidateExerciseIds);
        var previousCandidates = canPreserveAnswers
            ? HighIntensityCandidates.ToDictionary(candidate => candidate.ExerciseId)
            : new Dictionary<int, StrengthPlanHighIntensityCandidateDisplay>();

        var updatedCandidates = candidateSlots
            .GroupBy(candidate => candidate.Slot.ExerciseId!.Value)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                previousCandidates.TryGetValue(group.Key, out var previous);
                var name = group.Select(candidate => candidate.Slot.ExerciseName)
                    .FirstOrDefault(candidateName => !string.IsNullOrWhiteSpace(candidateName)) ??
                           UiTextCatalog.Get("Workout_Generation_ExercisePending");
                var assignments = group
                    .Select(candidate => candidate.Session.PlannedWeekday is { } weekday
                        ? StrengthPlanGenerationDisplayMapper.FullWeekdayLabel(weekday)
                        : StrengthPlanGenerationDisplayMapper.FormatLocalized(
                            "Workout_Generation_SessionTitleFormat",
                            candidate.Session.SessionIndex.ToString(CultureInfo.CurrentCulture)))
                    .Distinct(StringComparer.Ordinal);
                var assignmentSummary = StrengthPlanGenerationDisplayMapper.FormatLocalized(
                    "Workout_Generation_HeavyMainAssignmentFormat",
                    string.Join(", ", assignments));
                return new StrengthPlanHighIntensityCandidateDisplay(group.Key, name, assignmentSummary,
                    EvidenceYesNoChoices, previous?.FamiliarityChoice, previous?.ManageableLoadChoice);
            })
            .ToArray();

        _candidateReviewActivity = updatedCandidates.Length == NoPolicyReasons
            ? null
            : SelectedActivity?.Activity;
        _candidateReviewExerciseIds = candidateExerciseIds;
        ReplaceHighIntensityCandidates(updatedCandidates);
    }

    private void ClearHighIntensityCandidates()
    {
        _candidateReviewActivity = null;
        _candidateReviewExerciseIds = [];
        ReplaceHighIntensityCandidates([]);
    }

    private void ReplaceHighIntensityCandidates(
        IEnumerable<StrengthPlanHighIntensityCandidateDisplay> candidates)
    {
        foreach (var candidate in HighIntensityCandidates)
            candidate.PropertyChanged -= OnHighIntensityCandidatePropertyChanged;

        var nextCandidates = candidates.ToArray();
        HighIntensityCandidates = new ObservableCollection<StrengthPlanHighIntensityCandidateDisplay>(nextCandidates);
        foreach (var candidate in nextCandidates)
            candidate.PropertyChanged += OnHighIntensityCandidatePropertyChanged;

        OnPropertyChanged(nameof(HasHighIntensityCandidates));
        OnPropertyChanged(nameof(AreHighIntensityCandidateAnswersComplete));
        OnPropertyChanged(nameof(CanSubmitHighIntensityConfirmations));
        ConfirmHighIntensityExercisesCommand.NotifyCanExecuteChanged();
    }

    private void OnHighIntensityCandidatePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(StrengthPlanHighIntensityCandidateDisplay.FamiliarityChoice) or
            nameof(StrengthPlanHighIntensityCandidateDisplay.ManageableLoadChoice)))
            return;

        OnPropertyChanged(nameof(AreHighIntensityCandidateAnswersComplete));
        OnPropertyChanged(nameof(CanSubmitHighIntensityConfirmations));
        ConfirmHighIntensityExercisesCommand.NotifyCanExecuteChanged();
    }

    private bool CanAcceptProposal() => !IsBusy && CanSubmitProposal;

    private bool CanEditDetails() => !IsBusy && Preview is not null;

    private void EditDetails()
    {
        ClearPreview();
    }

    private async Task AcceptProposalAsync()
    {
        if (_proposal is null || !CanAcceptProposal())
        {
            StatusMessage = UiTextCatalog.Get("Workout_Generation_AcceptUnavailable");
            return;
        }

        IsBusy = true;
        StatusMessage = NoStatusMessage;
        try
        {
            var result = await _proposalClient.AcceptAsync(new AcceptStrengthPlanProposalRequestV020(
                _proposal.ProposalId));
            if (result.Status != "Accepted")
            {
                var statusKey = result.Status == "Conflict"
                    ? "Workout_Generation_PlanChanged"
                    : "Workout_Generation_AcceptFailed";
                ClearPreview(clearStatus: false);
                StatusMessage = UiTextCatalog.Get(statusKey);
                return;
            }

            _refreshNotifier.InvalidateWorkoutAndDashboard();
            await _navigateToTrainAsync();
        }
        catch (Exception)
        {
            StatusMessage = UiTextCatalog.Get("Workout_Generation_AcceptFailed");
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    partial void OnSelectedActivityChanged(StrengthPlanActivityChoice? value)
    {
        OnPropertyChanged(nameof(IsHighIntensityStrengthSelected));
        if (_historyActivity == value?.Activity)
            return;

        _historyActivity = value?.Activity;
        if (string.IsNullOrWhiteSpace(ConsistentWeeks))
            ConsistentWeeks = "24";
        if (string.IsNullOrWhiteSpace(AverageSessionsPerWeek))
            AverageSessionsPerWeek = "3";
        if (string.IsNullOrWhiteSpace(DaysSinceLastSession))
            DaysSinceLastSession = "2";
        if (string.IsNullOrWhiteSpace(WarmupMinutes))
            WarmupMinutes = "5";
        if (string.IsNullOrWhiteSpace(CooldownMinutes))
            CooldownMinutes = "5";
        ClearPreview();
    }

    private void ClearPreview(bool clearStatus = true)
    {
        _proposal = null;
        Preview = null;
        ClearHighIntensityCandidates();
        StrengthPreviewSessions.Clear();
        MixedActivitySessions.Clear();
        ActivityDecisions.Clear();
        ScheduleInputReasons.Clear();
        PreviewLimitations.Clear();
        if (clearStatus)
            StatusMessage = NoStatusMessage;

        NotifyPreviewChanged();
    }

    private void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(IsBeforePreview));
        OnPropertyChanged(nameof(HasFeasiblePreview));
        OnPropertyChanged(nameof(HasLimitation));
        OnPropertyChanged(nameof(HasMixedSchedule));
        OnPropertyChanged(nameof(HasMixedActivitySessions));
        OnPropertyChanged(nameof(HasNoMixedActivitySessions));
        OnPropertyChanged(nameof(HasActivityDecisions));
        OnPropertyChanged(nameof(HasScheduleInputReasons));
        OnPropertyChanged(nameof(HasStrengthPreviewSessions));
        OnPropertyChanged(nameof(HasHighIntensityCandidates));
        OnPropertyChanged(nameof(AreHighIntensityCandidateAnswersComplete));
        OnPropertyChanged(nameof(CanSubmitHighIntensityConfirmations));
        OnPropertyChanged(nameof(CanEditInputs));
        OnPropertyChanged(nameof(PreviewLimitationMessage));
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
        new(UiTextCatalog.Get("Workout_Generation_EvidenceYes"), true),
        new(UiTextCatalog.Get("Workout_Generation_EvidenceNo"), false)
    ];

    private static ObservableCollection<ActivityEvidenceAnswerChoice> CreateSwimmingStyleChoices() =>
    [
        new(UiTextCatalog.Get("Workout_Generation_SwimmingContinuous"), true),
        new(UiTextCatalog.Get("Workout_Generation_SwimmingIntervals"), false)
    ];

    private static ObservableCollection<ActivityEvidenceAnswerChoice> CreateYogaContentChoices() =>
    [
        new(UiTextCatalog.Get("Workout_Generation_YogaGentle"), false),
        new(UiTextCatalog.Get("Workout_Generation_YogaDemanding"), true)
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
        OnPropertyChanged(nameof(CanAnswerHighIntensityCandidates));
        OnPropertyChanged(nameof(CanSubmitHighIntensityConfirmations));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        CreateProposalCommand.NotifyCanExecuteChanged();
        ConfirmHighIntensityExercisesCommand.NotifyCanExecuteChanged();
        AcceptProposalCommand.NotifyCanExecuteChanged();
        EditDetailsCommand.NotifyCanExecuteChanged();
    }

    private static DayOfWeek[] NormalizeWeekdays(IReadOnlyList<int>? values) =>
        values is null
            ? []
            : values.Where(value => Enum.IsDefined(typeof(DayOfWeek), value))
                .Select(value => (DayOfWeek)value)
                .Distinct()
                .ToArray();
}

