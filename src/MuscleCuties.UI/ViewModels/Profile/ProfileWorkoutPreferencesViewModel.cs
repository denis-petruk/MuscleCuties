using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>The workout preferences page: activities, equipment and what the home or gym has, training weekdays and</summary>
/// excluded exercises — the inputs the plan generator reads.</summary>
public partial class ProfileWorkoutPreferencesViewModel : ObservableObject, IPageLoadAware
{
    public const string OnboardingWeekdaysContextKey = "onboarding-workout-weekdays";
    private const string FullGymEquipmentValue = "FullGym";
    private const string HomeDumbbellsBandsEquipmentValue = "HomeDumbbellsBands";
    private const string BodyweightEquipmentValue = "Bodyweight";

    private readonly Func<Task> _navigateAfterSaveAsync;
    private readonly IProfileWorkoutPreferencesUseCase _useCase;
    private readonly INavigationContextService? _navigationContext;
    private readonly IProfilePersonalInfoUseCase? _personalInfo;
    private int? _trainingExperienceLevel;
    internal const string StyleNeedsHistoryKey = "Workout_Generation_StyleNeedsHistory";
    internal const string DraftPreferencesContextKey = "workout-planning-draft-preferences";
    private readonly GetReviewedExerciseExclusionCatalogV020UseCase _exerciseExclusionCatalog;
    private Dictionary<int, int> _savedActivitySessionDurationsMinutes = new();
    private int _strengthSessionDurationMinutes =
        WorkoutPreferencesConstraints.DefaultSessionDurationMinutes(WorkoutActivityDto.GeneralStrength) ??
        WorkoutPreferencesConstraints.DefaultActivitySessionDurationMinutes;
    private string _savedPreferencesSnapshot = string.Empty;
    private bool _hasLoadedPreferences;
    private WorkoutPreferencesDto? _pendingLoadPreferences;
    private int[]? _pendingOnboardingWeekdays;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isLoadingData;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveActionText))]
    private bool _isPlanSetupNavigationPending;
    [ObservableProperty] private SelectionOption<string>? _selectedEquipmentOption;
    [ObservableProperty] private bool _gluteFocus;
    [ObservableProperty] private bool _hasDumbbells = true;
    [ObservableProperty] private bool _hasBands = true;
    [ObservableProperty] private bool _hasConfirmedBandAnchor;
    [ObservableProperty] private bool _hasSliders;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();
    [ObservableProperty] private ObservableCollection<WorkoutWeekdayOptionItem> _availableWeekdays = new();
    [ObservableProperty] private ObservableCollection<WorkoutExerciseExclusionOptionItem> _exerciseExclusionOptions = new();
    [ObservableProperty] private string _exerciseExclusionSearchText = string.Empty;
    [ObservableProperty] private bool _isExerciseExclusionCatalogLoaded;

    /// <summary>Takes the preferences use case and the navigation callbacks.</summary>
    public ProfileWorkoutPreferencesViewModel(
        IProfileWorkoutPreferencesUseCase useCase,
        GetReviewedExerciseExclusionCatalogV020UseCase exerciseExclusionCatalog,
        Func<Task> navigateBackAsync,
        Func<Task> navigateAfterSaveAsync,
        INavigationContextService? navigationContext = null,
        IProfilePersonalInfoUseCase? personalInfo = null)
    {
        _useCase = useCase;
        _personalInfo = personalInfo;
        _navigationContext = navigationContext;
        _exerciseExclusionCatalog = exerciseExclusionCatalog;
        _navigateAfterSaveAsync = navigateAfterSaveAsync;
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        ToggleWeekdayCommand = new RelayCommand<WorkoutWeekdayOptionItem>(ToggleWeekday);
        SwitchToGeneralStrengthCommand = new RelayCommand(SwitchToGeneralStrength);
        ToggleDumbbellsCommand = new RelayCommand(() => HasDumbbells = !HasDumbbells);
        ToggleBandsCommand = new RelayCommand(() => HasBands = !HasBands);
        ToggleBandAnchorCommand = new RelayCommand(() => HasConfirmedBandAnchor = !HasConfirmedBandAnchor);
        ToggleSlidersCommand = new RelayCommand(() => HasSliders = !HasSliders);
        ToggleGluteFocusCommand = new RelayCommand(() => GluteFocus = !GluteFocus);
        WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(new HashSet<WorkoutActivityDto>());
        AvailableWeekdays = new ObservableCollection<WorkoutWeekdayOptionItem>(CreateWeekdays()
            .Select(day => new WorkoutWeekdayOptionItem(day)));
        SelectedEquipmentOption = EquipmentOptions.First(option => option.Value == FullGymEquipmentValue);
    }


    private static readonly SelectionOption<string> BodyweightEquipmentOption =
        new(BodyweightEquipmentValue, "Profile_WorkoutPrefs_EquipmentBodyweight");

    /// <summary>
    /// Gate 60: All approved equipment profiles including bodyweight-only and explicit home hardware capabilities are offered.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SelectionOption<string>> _equipmentOptions = OfferedEquipmentOptions(null);

    /// <summary>The equipment options the saved level allows.</summary>
    internal static IReadOnlyList<SelectionOption<string>> OfferedEquipmentOptions(string? savedEquipmentLevel) =>
    [
        new(FullGymEquipmentValue, "Profile_WorkoutPrefs_EquipmentFullGym"),
        new(HomeDumbbellsBandsEquipmentValue, "Profile_WorkoutPrefs_EquipmentHome"),
        BodyweightEquipmentOption
    ];

    /// <summary>The extra gear toggles only apply to the home option, so they are hidden for a gym or bodyweight setup.</summary>
    public bool ShowHomeEquipmentCapabilities =>
        string.Equals(SelectedEquipmentOption?.Value, HomeDumbbellsBandsEquipmentValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a full gym was chosen, which decides what still has to be confirmed below.</summary>
    public bool IsFullGymEquipment =>
        string.Equals(SelectedEquipmentOption?.Value, FullGymEquipmentValue, StringComparison.OrdinalIgnoreCase);

    // A full gym includes bands, but the anchor is never inferred: the user must confirm it here
    // just as a home user does. Hiding this for FullGym made the planner's anchor requirement
    // impossible to satisfy.
    /// <summary>A full gym includes bands, but the anchor is never inferred: the confirmation is shown there too, because a</summary>
    /// planner that needs an anchor cannot proceed without one.</summary>
    public bool ShowBandAnchorConfirmation => IsFullGymEquipment || (ShowHomeEquipmentCapabilities && HasBands);

    /// <summary>C1/OD-7: a saved strength style that is no longer offered for this profile.</summary>
    public bool HasIneligibleSavedStyle =>
        WorkoutActivityOptions.Any(option => option.IsSelected && option.HasUnavailableReason);

    /// <summary>Switches the focus back to general strength.</summary>
    public RelayCommand SwitchToGeneralStrengthCommand { get; }
    /// <summary>Toggles the dumbbell equipment.</summary>
    public RelayCommand ToggleDumbbellsCommand { get; }
    /// <summary>Toggles the band equipment.</summary>
    public RelayCommand ToggleBandsCommand { get; }
    /// <summary>Toggles the band anchor.</summary>
    public RelayCommand ToggleBandAnchorCommand { get; }
    /// <summary>Toggles the sliders.</summary>
    public RelayCommand ToggleSlidersCommand { get; }
    /// <summary>Toggles the glute focus.</summary>
    public RelayCommand ToggleGluteFocusCommand { get; }

    /// <summary>The activities grouped into the picker's sections.</summary>
    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions =>
        ProfileWorkoutActivityOptions.BuildGroups(WorkoutActivityOptions);

    /// <summary>How many training days are chosen, shown next to the weekday row.</summary>
    public int SelectedWeekdayCount => AvailableWeekdays.Count(day => day.IsSelected);
    /// <summary>True while the page loads.</summary>
    public bool IsPageLoading => IsLoadingData;
    /// <summary>Compares the current inputs with the snapshot taken when they were loaded or saved, so a change and its undo</summary>
    /// read correctly.</summary>
    public bool HasUnsavedChanges => _hasLoadedPreferences &&
        !string.Equals(_savedPreferencesSnapshot, CapturePreferencesSnapshot(), StringComparison.Ordinal);
    /// <summary>True when the exclusion search has text.</summary>
    public bool HasExerciseExclusionQuery => !string.IsNullOrWhiteSpace(ExerciseExclusionSearchText);
    /// <summary>True when the search matched no exercises.</summary>
    public bool HasNoExerciseExclusionMatches => IsExerciseExclusionCatalogLoaded && HasExerciseExclusionQuery &&
        FilteredExerciseExclusionOptions.Count == 0;
    /// <summary>The search results, listing only exercises not already excluded so the list never offers a duplicate.</summary>
    public IReadOnlyList<WorkoutExerciseExclusionOptionItem> FilteredExerciseExclusionOptions =>
        !HasExerciseExclusionQuery
            ? []
            : ExerciseExclusionOptions.Where(option =>
                    !option.IsSelected && option.Name.Contains(ExerciseExclusionSearchText.Trim(), StringComparison.CurrentCultureIgnoreCase))
                .ToArray();
    /// <summary>The exercises currently kept out of generated plans.</summary>
    public IReadOnlyList<WorkoutExerciseExclusionOptionItem> SelectedExerciseExclusions =>
        ExerciseExclusionOptions.Where(option => option.IsSelected).ToArray();
    /// <summary>True when the preferences can be saved.</summary>
    public bool CanSave => !IsBusy && IsExerciseExclusionCatalogLoaded;
    /// <summary>The button says what it will do: continue to the plan setup when there are changes to save, otherwise just</summary>
    /// open it.</summary>
    public string SaveActionText => UiTextCatalog.Get(IsPlanSetupNavigationPending && !HasUnsavedChanges
        ? "Profile_WorkoutPrefs_OpenPlanSetup"
        : "Profile_WorkoutPrefs_ContinueToPlanSetup");

    /// <summary>Returns to the profile.</summary>
    public AsyncRelayCommand BackCommand { get; }
    /// <summary>Loads the preferences.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>Saves the preferences.</summary>
    public AsyncRelayCommand SaveCommand { get; }
    /// <summary>Toggles a workout activity.</summary>
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    /// <summary>Toggles a training weekday.</summary>
    public RelayCommand<WorkoutWeekdayOptionItem> ToggleWeekdayCommand { get; }

    /// <summary>Loads the saved preferences and the exercise catalog together, and keeps the loaded inputs as the baseline for</summary>
    /// the unsaved-changes flag.</summary>
    private async Task LoadDataAsync()
    {
        IsLoadError = false;
        IsLoadingData = true;
        IsBusy = true;
        IsExerciseExclusionCatalogLoaded = false;
        StatusMessage = string.Empty;
        try
        {
            // A draft only applies to the navigation that supplied it; the cached copy made the
            // editor keep showing preferences the user had since changed.
            _pendingLoadPreferences = null;
            if (_navigationContext?.TryTake<WorkoutPreferencesDto>(DraftPreferencesContextKey, out var draft) == true)
                _pendingLoadPreferences = draft;
            var preferences = _pendingLoadPreferences ?? await _useCase.LoadAsync();
            _pendingLoadPreferences = preferences;
            var selectedTypes = preferences?.ActivityTypes
                                   .Where(value => Enum.IsDefined(typeof(WorkoutActivityDto), value))
                                   .Select(value => (WorkoutActivityDto)value).ToHashSet()
                               ?? new HashSet<WorkoutActivityDto>();
            WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(selectedTypes);
            _trainingExperienceLevel = await TryLoadTrainingExperienceLevelAsync();
            ApplyStyleEligibility();
            _savedActivitySessionDurationsMinutes = (preferences?.ActivitySessionDurationsMinutes ??
                    new Dictionary<int, int>())
                .Where(pair => Enum.IsDefined(typeof(WorkoutActivityDto), pair.Key) &&
                               WorkoutPreferencesConstraints.IsValidSessionDuration(
                                   (WorkoutActivityDto)pair.Key, pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            _strengthSessionDurationMinutes = _savedActivitySessionDurationsMinutes
                .Where(pair => WorkoutPlanningConstants.IsStrengthActivity((WorkoutActivityDto)pair.Key))
                .Select(pair => (int?)pair.Value)
                .FirstOrDefault() ??
                WorkoutPreferencesConstraints.DefaultSessionDurationMinutes(WorkoutActivityDto.GeneralStrength) ??
                WorkoutPreferencesConstraints.DefaultActivitySessionDurationMinutes;

            var selectedDays = preferences?.AvailableWeekdays
                                   .Where(value => Enum.IsDefined(typeof(DayOfWeek), value))
                                   .Select(value => (DayOfWeek)value).ToHashSet()
                               ?? new HashSet<DayOfWeek>();
            if (_navigationContext?.TryTake<int[]>(OnboardingWeekdaysContextKey, out var onboardingDays) == true)
                _pendingOnboardingWeekdays = onboardingDays;
            if (_pendingOnboardingWeekdays is not null)
                selectedDays = _pendingOnboardingWeekdays.Where(value => Enum.IsDefined(typeof(DayOfWeek), value))
                    .Select(value => (DayOfWeek)value).ToHashSet();
            AvailableWeekdays = new ObservableCollection<WorkoutWeekdayOptionItem>(CreateWeekdays()
                .Select(day => new WorkoutWeekdayOptionItem(day)
                {
                    IsSelected = selectedDays.Contains(day)
                }));

            EquipmentOptions = OfferedEquipmentOptions(preferences?.EquipmentLevel);
            SelectedEquipmentOption = EquipmentOptions.FirstOrDefault(option =>
                                          string.Equals(option.Value, preferences?.EquipmentLevel,
                                              StringComparison.OrdinalIgnoreCase)) ??
                                      EquipmentOptions.First(option => option.Value == FullGymEquipmentValue);
            GluteFocus = preferences?.GluteFocus ?? false;
            HasDumbbells = preferences?.HasDumbbells ?? true;
            HasBands = preferences?.HasBands ?? true;
            HasConfirmedBandAnchor = preferences?.HasConfirmedBandAnchor ?? false;
            HasSliders = preferences?.HasSliders ?? false;

            var excludedExerciseIds = preferences?.ExcludedExerciseIds ?? new HashSet<int>();
            var exclusionOptions = await _exerciseExclusionCatalog.ExecuteAsync();
            ExerciseExclusionOptions = new ObservableCollection<WorkoutExerciseExclusionOptionItem>(
                exclusionOptions.Select(option => new WorkoutExerciseExclusionOptionItem(
                    option.ExerciseId, UiTextCatalog.TranslateRuntimeText(option.Name))
                {
                    IsSelected = excludedExerciseIds.Contains(option.ExerciseId)
                }));
            foreach (var option in ExerciseExclusionOptions)
                option.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName != nameof(WorkoutExerciseExclusionOptionItem.IsSelected)) return;
                    OnPropertyChanged(nameof(SelectedExerciseExclusions));
                    OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));
                    OnPropertyChanged(nameof(HasNoExerciseExclusionMatches));
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                };
            IsExerciseExclusionCatalogLoaded = true;
            _pendingLoadPreferences = null;
            _pendingOnboardingWeekdays = null;
            _hasLoadedPreferences = true;
            _savedPreferencesSnapshot = CapturePreferencesSnapshot();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
            OnPropertyChanged(nameof(SelectedWeekdayCount));
            OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));
            OnPropertyChanged(nameof(SelectedExerciseExclusions));
            OnPropertyChanged(nameof(HasNoExerciseExclusionMatches));
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[ProfileWorkoutPreferencesViewModel] Catalog load failed ({exception.GetType().Name}).");
            IsExerciseExclusionCatalogLoaded = false;
            IsLoadError = true;
            StatusMessage = UiTextCatalog.Get("Profile_WorkoutPrefs_ExerciseCatalogUnavailable");
        }
        finally
        {
            IsLoadingData = false;
            IsBusy = false;
            OnPropertyChanged(nameof(CanSave));
        }
    }

    /// <summary>Saves the preferences, moves the baseline and carries on to the plan setup; refused input stays on the page as</summary>
    /// errors.</summary>
    private async Task SaveAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = string.Empty;
        var settingsSaved = false;
        try
        {
            if (!IsExerciseExclusionCatalogLoaded)
            {
                StatusMessage = UiTextCatalog.Get("Profile_WorkoutPrefs_ExerciseCatalogUnavailable");
                return;
            }

            var selectedActivities = WorkoutActivityOptions.Where(option => option.IsSelected).ToArray();
            var availableWeekdays = AvailableWeekdays.Where(day => day.IsSelected)
                .Select(day => (int)day.DayOfWeek).ToArray();
            var validationErrors = CollectValidationErrors(selectedActivities, availableWeekdays);
            if (validationErrors.Count > 0)
            {
                StatusMessage = string.Join(" ", validationErrors);
                return;
            }

            var isHomeEquipment = string.Equals(SelectedEquipmentOption!.Value, HomeDumbbellsBandsEquipmentValue,
                StringComparison.OrdinalIgnoreCase);

            var draftPreferences = new WorkoutPreferencesDto(
                selectedActivities.Select(option => (int)option.ActivityType).ToArray(),
                availableWeekdays,
                selectedActivities
                    .Where(option => WorkoutPreferencesConstraints.ActivityRequiresSessionDuration(
                        option.ActivityType))
                    .ToDictionary(option => (int)option.ActivityType,
                        option => ResolveSessionDurationMinutes(option.ActivityType)),
                SelectedEquipmentOption.Value,
                selectedActivities
                    .Where(option => WorkoutPreferencesConstraints.ActivityRequiresExplicitWeeklyFrequency(
                        option.ActivityType))
                    .ToDictionary(option => (int)option.ActivityType,
                        option => WorkoutPreferencesConstraints.DefaultWeeklyFrequency(
                            option.ActivityType, availableWeekdays.Length)!.Value),
                ExerciseExclusionOptions.Where(option => option.IsSelected)
                    .Select(option => option.ExerciseId).ToHashSet(),
                GluteFocus: GluteFocus,
                HasSliders: HasSliders,
                HasConfirmedBandAnchor: HasConfirmedBandAnchor && (isHomeEquipment ? HasBands : IsFullGymEquipment),
                HasDumbbells: isHomeEquipment ? HasDumbbells : null,
                HasBands: isHomeEquipment ? HasBands : null);
            if (!await _useCase.SaveAsync(draftPreferences).ConfigureAwait(true))
            {
                StatusMessage = UiTextCatalog.Get("Profile_WorkoutPrefs_SaveServerUnavailable");
                return;
            }

            if (_navigationContext is null)
                throw new InvalidOperationException("Workout planning draft context is unavailable.");
            _navigationContext.Set(DraftPreferencesContextKey, draftPreferences);
            settingsSaved = true;
            _savedPreferencesSnapshot = CapturePreferencesSnapshot();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            await OpenPlanSetupAsync();
            return;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileWorkoutPreferencesViewModel] Save failed ({ex.GetType().Name}).");
            StatusMessage = !settingsSaved
                ? UiTextCatalog.Get("Profile_WorkoutPrefs_DraftUnavailable")
                : UiTextCatalog.Get("Profile_WorkoutPrefs_PlanSetupCouldNotOpen");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSave));
        }
    }

    /// <summary>The reasons the preferences cannot be saved.</summary>
    private List<string> CollectValidationErrors(
        WorkoutActivityOptionItem[] selectedActivities,
        int[] availableWeekdays)
    {
        var errors = new List<string>();
        if (!selectedActivities.Any(option => WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
            errors.Add(UiTextCatalog.Get("Profile_WorkoutPrefs_AtLeastOneStrengthActivityIs"));

        if (availableWeekdays.Length is < WorkoutPreferencesConstraints.MinimumAvailableWeekdays or >
            WorkoutPreferencesConstraints.MaximumAvailableWeekdays)
            errors.Add(UiTextCatalog.Get("Profile_WorkoutPrefs_PickAtLeastTwoDays"));

        if (SelectedEquipmentOption is null)
        {
            errors.Add(UiTextCatalog.Get("Profile_WorkoutPrefs_ChooseEquipment"));
        }
        else if (string.Equals(SelectedEquipmentOption.Value, HomeDumbbellsBandsEquipmentValue,
                     StringComparison.OrdinalIgnoreCase) && !HasDumbbells && !HasBands)
        {
            errors.Add(UiTextCatalog.Get("Profile_WorkoutPrefs_HomeNeedsEquipment"));
        }

        return errors;
    }

    /// <summary>Opens the plan setup and marks that navigation as pending, so the button keeps the label it was tapped with</summary>
    /// while the page changes.</summary>
    private async Task OpenPlanSetupAsync()
    {
        IsPlanSetupNavigationPending = true;
        await _navigateAfterSaveAsync();
        IsPlanSetupNavigationPending = false;
    }

    /// <summary>Refreshes the derived state whenever a property changes.</summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(HasUnsavedChanges))
            OnPropertyChanged(nameof(SaveActionText));
    }

    /// <summary>Adds or removes one activity, keeping the list free of duplicates.</summary>
    private void ToggleWorkoutActivity(WorkoutActivityOptionItem? item)
    {
        if (item is null)
            return;

        var toggleResult = ProfileWorkoutActivityOptions.ToggleSelection(WorkoutActivityOptions, item);
        StatusMessage = toggleResult == "At least one strength activity is required for your plan."
            ? UiTextCatalog.Get("Profile_WorkoutPrefs_AtLeastOneStrengthActivityIs")
            : toggleResult == "Choose at least one activity."
                ? UiTextCatalog.Get("Onboarding_ProfileSetup_StrengthActivityRequired")
                : toggleResult;
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasIneligibleSavedStyle));
    }

    /// <summary>Falls back to general strength when the saved style is no longer offered for this profile, so the page opens</summary>
    /// on a style that exists.</summary>
    private void SwitchToGeneralStrength()
    {
        foreach (var option in WorkoutActivityOptions)
        {
            if (option.ActivityType == WorkoutActivityDto.GeneralStrength)
                option.IsSelected = true;
            else if (option.IsSelected && option.HasUnavailableReason)
                option.IsSelected = false;
        }

        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasIneligibleSavedStyle));
    }

    /// <summary>The training experience level the style eligibility needs, or nothing when it cannot be read.</summary>
    private async Task<int?> TryLoadTrainingExperienceLevelAsync()
    {
        if (_personalInfo is null)
            return null;
        try
        {
            return (await _personalInfo.LoadAsync()).Profile?.TrainingExperienceLevel;
        }
        catch (Exception exception)
        {
            // The server stays authoritative; an unavailable profile restricts nothing here.
            Trace.WriteLine($"[ProfileWorkoutPreferencesViewModel] Experience load failed ({exception.GetType().Name}).");
            return null;
        }
    }

    /// <summary>Hides the styles this profile cannot use and switches away from one that is already selected.</summary>
    private void ApplyStyleEligibility()
    {
        foreach (var option in WorkoutActivityOptions.Where(option =>
                     WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
            option.UnavailableReason = StrengthStyleChoiceEligibilityProjection.IsOffered(
                option.ActivityType, _trainingExperienceLevel)
                ? string.Empty
                : UiTextCatalog.Get(StyleNeedsHistoryKey);
        OnPropertyChanged(nameof(HasIneligibleSavedStyle));
    }

    /// <summary>Turns one training day on or off.</summary>
    private void ToggleWeekday(WorkoutWeekdayOptionItem? item)
    {
        if (item is null)
            return;

        item.IsSelected = !item.IsSelected;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedWeekdayCount));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>The session length to save for an activity, falling back to the page's current length when the activity has</summary>
    /// no answer of its own.</summary>
    private int ResolveSessionDurationMinutes(WorkoutActivityDto activityType)
    {
        if (_savedActivitySessionDurationsMinutes.TryGetValue((int)activityType, out var savedDuration) &&
            WorkoutPreferencesConstraints.IsValidSessionDuration(activityType, savedDuration))
            return savedDuration;

        if (WorkoutPlanningConstants.IsStrengthActivity(activityType) &&
            WorkoutPreferencesConstraints.IsValidSessionDuration(activityType, _strengthSessionDurationMinutes))
            return _strengthSessionDurationMinutes;

        return WorkoutPreferencesConstraints.DefaultSessionDurationMinutes(activityType) ??
               WorkoutPreferencesConstraints.DefaultActivitySessionDurationMinutes;
    }

    /// <summary>Every editable input as one canonical string, which is what the unsaved-changes comparison reads.</summary>
    private string CapturePreferencesSnapshot()
    {
        var selectedActivities = string.Join(",", WorkoutActivityOptions
            .Where(option => option.IsSelected)
            .Select(option => ((int)option.ActivityType).ToString(CultureInfo.InvariantCulture))
            .OrderBy(value => value, StringComparer.Ordinal));
        var selectedWeekdays = string.Join(",", AvailableWeekdays
            .Where(day => day.IsSelected)
            .Select(day => ((int)day.DayOfWeek).ToString(CultureInfo.InvariantCulture))
            .OrderBy(value => value, StringComparer.Ordinal));
        var excludedExercises = string.Join(",", ExerciseExclusionOptions
            .Where(option => option.IsSelected)
            .Select(option => option.ExerciseId.ToString(CultureInfo.InvariantCulture))
            .OrderBy(value => value, StringComparer.Ordinal));

        return string.Join("|",
            SelectedEquipmentOption?.Value ?? string.Empty,
            GluteFocus.ToString(CultureInfo.InvariantCulture),
            HasDumbbells.ToString(CultureInfo.InvariantCulture),
            HasBands.ToString(CultureInfo.InvariantCulture),
            HasConfirmedBandAnchor.ToString(CultureInfo.InvariantCulture),
            HasSliders.ToString(CultureInfo.InvariantCulture),
            selectedActivities,
            selectedWeekdays,
            excludedExercises);
    }

    /// <summary>The weekdays the picker offers.</summary>
    private static IReadOnlyList<DayOfWeek> CreateWeekdays() =>
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    /// <summary>Follows the activity options when they are replaced.</summary>
    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(HasIneligibleSavedStyle));
    }

    /// <summary>Follows the exclusion options when they are replaced.</summary>
    partial void OnExerciseExclusionOptionsChanged(ObservableCollection<WorkoutExerciseExclusionOptionItem> value)
    {
        OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));
        OnPropertyChanged(nameof(SelectedExerciseExclusions));
        OnPropertyChanged(nameof(HasNoExerciseExclusionMatches));
    }

    /// <summary>Refreshes the filtered exclusions when the search changes.</summary>
    partial void OnExerciseExclusionSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasExerciseExclusionQuery));
        OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));
        OnPropertyChanged(nameof(HasNoExerciseExclusionMatches));
    }

    /// <summary>Refreshes the save state when busy changes.</summary>
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    /// <summary>Refreshes the save state when the catalog loads.</summary>
    partial void OnIsExerciseExclusionCatalogLoadedChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    /// <summary>Refreshes the loading flag when loading changes.</summary>
    partial void OnIsLoadingDataChanged(bool value) => OnPropertyChanged(nameof(IsPageLoading));

    /// <summary>Applies the chosen equipment level.</summary>
    partial void OnSelectedEquipmentOptionChanged(SelectionOption<string>? value)
    {
        OnPropertyChanged(nameof(ShowHomeEquipmentCapabilities));
        OnPropertyChanged(nameof(IsFullGymEquipment));
        OnPropertyChanged(nameof(ShowBandAnchorConfirmation));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Refreshes the anchor state when dumbbells change.</summary>
    partial void OnHasDumbbellsChanged(bool value) =>
        OnPropertyChanged(nameof(HasUnsavedChanges));

    /// <summary>Clears the anchor when the bands are dropped.</summary>
    partial void OnHasBandsChanged(bool value)
    {
        if (!value)
            HasConfirmedBandAnchor = false;
        OnPropertyChanged(nameof(ShowBandAnchorConfirmation));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Refreshes the anchor state when it changes.</summary>
    partial void OnHasConfirmedBandAnchorChanged(bool value) =>
        OnPropertyChanged(nameof(HasUnsavedChanges));

    /// <summary>Refreshes the equipment flags when sliders change.</summary>
    partial void OnHasSlidersChanged(bool value) =>
        OnPropertyChanged(nameof(HasUnsavedChanges));

    /// <summary>Refreshes the equipment flags when the focus changes.</summary>
    partial void OnGluteFocusChanged(bool value) =>
        OnPropertyChanged(nameof(HasUnsavedChanges));
}
