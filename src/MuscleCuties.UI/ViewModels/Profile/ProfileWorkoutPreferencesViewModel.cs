using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileWorkoutPreferencesViewModel : ObservableObject, IPageLoadAware
{
    public const string OnboardingWeekdaysContextKey = "onboarding-workout-weekdays";
    private const string FullGymEquipmentValue = "FullGym";
    private const string HomeDumbbellsBandsEquipmentValue = "HomeDumbbellsBands";
    private const string BodyweightEquipmentValue = "Bodyweight";

    private readonly Func<Task> _navigateAfterSaveAsync;
    private readonly IProfileRefreshNotifier _refreshNotifier;
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
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SaveActionText))]
    private bool _isPlanSetupNavigationPending;
    [ObservableProperty] private SelectionOption<string>? _selectedEquipmentOption;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();
    [ObservableProperty] private ObservableCollection<WorkoutWeekdayOptionItem> _availableWeekdays = new();
    [ObservableProperty] private ObservableCollection<WorkoutExerciseExclusionOptionItem> _exerciseExclusionOptions = new();
    [ObservableProperty] private string _exerciseExclusionSearchText = string.Empty;
    [ObservableProperty] private bool _isExerciseExclusionCatalogLoaded;

    public ProfileWorkoutPreferencesViewModel(
        IProfileWorkoutPreferencesUseCase useCase,
        GetReviewedExerciseExclusionCatalogV020UseCase exerciseExclusionCatalog,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateBackAsync,
        Func<Task> navigateAfterSaveAsync,
        INavigationContextService? navigationContext = null,
        IProfilePersonalInfoUseCase? personalInfo = null)
    {
        _useCase = useCase;
        _personalInfo = personalInfo;
        _navigationContext = navigationContext;
        _exerciseExclusionCatalog = exerciseExclusionCatalog;
        _refreshNotifier = refreshNotifier;
        _navigateAfterSaveAsync = navigateAfterSaveAsync;
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        ToggleWeekdayCommand = new RelayCommand<WorkoutWeekdayOptionItem>(ToggleWeekday);
        SwitchToGeneralStrengthCommand = new RelayCommand(SwitchToGeneralStrength);
        WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(new HashSet<WorkoutActivityDto>());
        AvailableWeekdays = new ObservableCollection<WorkoutWeekdayOptionItem>(CreateWeekdays()
            .Select(day => new WorkoutWeekdayOptionItem(day)));
        SelectedEquipmentOption = EquipmentOptions.First(option => option.Value == FullGymEquipmentValue);
    }


    private static readonly SelectionOption<string> BodyweightEquipmentOption =
        new(BodyweightEquipmentValue, "Profile_WorkoutPrefs_EquipmentBodyweight");

    /// <summary>
    /// D1/OD-2: "Bodyweight only" is not offered for new selections in the beta (the reviewed catalog
    /// has no bodyweight squat, lunge or pull). A saved bodyweight setting is still listed so it is
    /// displayed and preserved; the server keeps planning it.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SelectionOption<string>> _equipmentOptions = OfferedEquipmentOptions(null);

    internal static IReadOnlyList<SelectionOption<string>> OfferedEquipmentOptions(string? savedEquipmentLevel)
    {
        List<SelectionOption<string>> options =
        [
            new(FullGymEquipmentValue, "Profile_WorkoutPrefs_EquipmentFullGym"),
            new(HomeDumbbellsBandsEquipmentValue, "Profile_WorkoutPrefs_EquipmentHome")
        ];
        if (string.Equals(savedEquipmentLevel, BodyweightEquipmentValue, StringComparison.OrdinalIgnoreCase))
            options.Add(BodyweightEquipmentOption);
        return options;
    }

    /// <summary>C1/OD-7: a saved strength style that is no longer offered for this profile.</summary>
    public bool HasIneligibleSavedStyle =>
        WorkoutActivityOptions.Any(option => option.IsSelected && option.HasUnavailableReason);

    public RelayCommand SwitchToGeneralStrengthCommand { get; }

    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions =>
        ProfileWorkoutActivityOptions.BuildGroups(WorkoutActivityOptions);

    public int SelectedWeekdayCount => AvailableWeekdays.Count(day => day.IsSelected);
    public bool IsPageLoading => IsLoadingData;
    public bool HasUnsavedChanges => _hasLoadedPreferences &&
        !string.Equals(_savedPreferencesSnapshot, CapturePreferencesSnapshot(), StringComparison.Ordinal);
    public bool HasExerciseExclusionQuery => !string.IsNullOrWhiteSpace(ExerciseExclusionSearchText);
    public bool HasNoExerciseExclusionMatches => IsExerciseExclusionCatalogLoaded && HasExerciseExclusionQuery &&
        FilteredExerciseExclusionOptions.Count == 0;
    public IReadOnlyList<WorkoutExerciseExclusionOptionItem> FilteredExerciseExclusionOptions =>
        !HasExerciseExclusionQuery
            ? []
            : ExerciseExclusionOptions.Where(option =>
                    !option.IsSelected && option.Name.Contains(ExerciseExclusionSearchText.Trim(), StringComparison.CurrentCultureIgnoreCase))
                .ToArray();
    public IReadOnlyList<WorkoutExerciseExclusionOptionItem> SelectedExerciseExclusions =>
        ExerciseExclusionOptions.Where(option => option.IsSelected).ToArray();
    public bool CanSave => !IsBusy && IsExerciseExclusionCatalogLoaded;
    public string SaveActionText => UiTextCatalog.Get(IsPlanSetupNavigationPending && !HasUnsavedChanges
        ? "Profile_WorkoutPrefs_OpenPlanSetup"
        : "Profile_WorkoutPrefs_ContinueToPlanSetup");

    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    public RelayCommand<WorkoutWeekdayOptionItem> ToggleWeekdayCommand { get; }

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

            var excludedExerciseIds = preferences?.ExcludedExerciseIds ?? new HashSet<int>();
            var exclusionOptions = await _exerciseExclusionCatalog.ExecuteAsync();
            ExerciseExclusionOptions = new ObservableCollection<WorkoutExerciseExclusionOptionItem>(
                exclusionOptions.Select(option => new WorkoutExerciseExclusionOptionItem(
                    option.ExerciseId, option.Name)
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
            if (!selectedActivities.Any(option => WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
            {
                StatusMessage = UiTextCatalog.Get("Profile_WorkoutPrefs_AtLeastOneStrengthActivityIs");
                return;
            }

            if (availableWeekdays.Length is < WorkoutPreferencesConstraints.MinimumAvailableWeekdays or >
                WorkoutPreferencesConstraints.MaximumAvailableWeekdays)
            {
                StatusMessage = UiTextCatalog.Get("Profile_WorkoutPrefs_PickAtLeastTwoDays");
                return;
            }

            if (SelectedEquipmentOption is null)
            {
                StatusMessage = UiTextCatalog.Get("Profile_WorkoutPrefs_ChooseEquipment");
                return;
            }

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
                    .Select(option => option.ExerciseId).ToHashSet());
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

    private async Task OpenPlanSetupAsync()
    {
        IsPlanSetupNavigationPending = true;
        await _navigateAfterSaveAsync();
        IsPlanSetupNavigationPending = false;
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(HasUnsavedChanges))
            OnPropertyChanged(nameof(SaveActionText));
    }

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

    private void ToggleWeekday(WorkoutWeekdayOptionItem? item)
    {
        if (item is null)
            return;

        item.IsSelected = !item.IsSelected;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedWeekdayCount));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

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
            selectedActivities,
            selectedWeekdays,
            excludedExercises);
    }

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

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(HasIneligibleSavedStyle));
    }

    partial void OnExerciseExclusionOptionsChanged(ObservableCollection<WorkoutExerciseExclusionOptionItem> value)
    {
        OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));
        OnPropertyChanged(nameof(SelectedExerciseExclusions));
        OnPropertyChanged(nameof(HasNoExerciseExclusionMatches));
    }

    partial void OnExerciseExclusionSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasExerciseExclusionQuery));
        OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));
        OnPropertyChanged(nameof(HasNoExerciseExclusionMatches));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    partial void OnIsExerciseExclusionCatalogLoadedChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    partial void OnIsLoadingDataChanged(bool value) => OnPropertyChanged(nameof(IsPageLoading));

    partial void OnSelectedEquipmentOptionChanged(SelectionOption<string>? value) =>
        OnPropertyChanged(nameof(HasUnsavedChanges));
}
