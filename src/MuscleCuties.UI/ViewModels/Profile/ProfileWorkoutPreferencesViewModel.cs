using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileWorkoutPreferencesViewModel : ObservableObject, IPageLoadAware
{
    private const string FullGymEquipmentValue = "FullGym";
    private const string HomeDumbbellsBandsEquipmentValue = "HomeDumbbellsBands";
    private const string BodyweightEquipmentValue = "Bodyweight";

    private readonly Func<Task> _navigateAfterSaveAsync;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly IProfileWorkoutPreferencesUseCase _useCase;
    private readonly GetReviewedExerciseExclusionCatalogV020UseCase _exerciseExclusionCatalog;
    private Dictionary<int, int> _savedActivitySessionDurationsMinutes = new();
    private int _strengthSessionDurationMinutes =
        WorkoutPreferencesConstraints.DefaultSessionDurationMinutes(WorkoutActivityDto.GeneralStrength) ??
        WorkoutPreferencesConstraints.DefaultActivitySessionDurationMinutes;
    private string _savedPreferencesSnapshot = string.Empty;
    private bool _hasLoadedPreferences;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isLoadingData;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isUpdatingPlan;
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
        Func<Task> navigateAfterSaveAsync)
    {
        _useCase = useCase;
        _exerciseExclusionCatalog = exerciseExclusionCatalog;
        _refreshNotifier = refreshNotifier;
        _navigateAfterSaveAsync = navigateAfterSaveAsync;
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        ToggleWeekdayCommand = new RelayCommand<WorkoutWeekdayOptionItem>(ToggleWeekday);
        WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(new HashSet<WorkoutActivityDto>());
        AvailableWeekdays = new ObservableCollection<WorkoutWeekdayOptionItem>(CreateWeekdays()
            .Select(day => new WorkoutWeekdayOptionItem(day)));
        SelectedEquipmentOption = EquipmentOptions.First(option => option.Value == FullGymEquipmentValue);
    }

    public IReadOnlyList<SelectionOption<string>> EquipmentOptions { get; } =
    [
        new(FullGymEquipmentValue, "Profile_WorkoutPrefs_EquipmentFullGym"),
        new(HomeDumbbellsBandsEquipmentValue, "Profile_WorkoutPrefs_EquipmentHome"),
        new(BodyweightEquipmentValue, "Profile_WorkoutPrefs_EquipmentBodyweight")
    ];

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
            var preferences = await _useCase.LoadAsync();
            var selectedTypes = preferences?.ActivityTypes
                                   .Where(value => Enum.IsDefined(typeof(WorkoutActivityDto), value))
                                   .Select(value => (WorkoutActivityDto)value).ToHashSet()
                               ?? new HashSet<WorkoutActivityDto>();
            WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(selectedTypes);
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
            AvailableWeekdays = new ObservableCollection<WorkoutWeekdayOptionItem>(CreateWeekdays()
                .Select(day => new WorkoutWeekdayOptionItem(day)
                {
                    IsSelected = selectedDays.Contains(day)
                }));

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
            StatusMessage = "Profile_WorkoutPrefs_ExerciseCatalogUnavailable";
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
        IsBusy = true;
        StatusMessage = string.Empty;
        var settingsSaved = false;
        try
        {
            if (!IsExerciseExclusionCatalogLoaded)
            {
                StatusMessage = "Profile_WorkoutPrefs_ExerciseCatalogUnavailable";
                return;
            }

            var selectedActivities = WorkoutActivityOptions.Where(option => option.IsSelected).ToArray();
            var availableWeekdays = AvailableWeekdays.Where(day => day.IsSelected)
                .Select(day => (int)day.DayOfWeek).ToArray();
            if (selectedActivities.Length == 0)
            {
                StatusMessage = "Choose at least one workout activity.";
                return;
            }

            if (!selectedActivities.Any(option => WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
            {
                StatusMessage = "Profile_WorkoutPrefs_AtLeastOneStrengthActivityIs";
                return;
            }

            if (availableWeekdays.Length is < WorkoutPreferencesConstraints.MinimumAvailableWeekdays or >
                WorkoutPreferencesConstraints.MaximumAvailableWeekdays)
            {
                StatusMessage = "Choose between 2 and 7 available weekdays.";
                return;
            }

            if (SelectedEquipmentOption is null)
            {
                StatusMessage = "Profile_WorkoutPrefs_ChooseEquipment";
                return;
            }

            IsUpdatingPlan = true;
            var result = await _useCase.SaveAndUpdatePlanAsync(new WorkoutPreferencesDto(
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
                    .Select(option => option.ExerciseId).ToHashSet()));
            if (result.Error == WorkoutPreferencesError.ProfileMissing)
            {
                StatusMessage = "Complete personal info before workout preferences.";
                return;
            }

            if (result.Error == WorkoutPreferencesError.InvalidPreferences)
            {
                StatusMessage = "Profile_WorkoutPrefs_InvalidPreferences";
                return;
            }

            if (result.Error == WorkoutPreferencesError.PlanUpdateFailed)
            {
                StatusMessage = "Profile_WorkoutPrefs_PlanUpdateFailed";
                return;
            }

            if (result.Error != WorkoutPreferencesError.None)
            {
                StatusMessage = result.Error switch
                {
                    WorkoutPreferencesError.Offline => "Profile_WorkoutPrefs_SaveOffline",
                    WorkoutPreferencesError.SignInRequired => "Profile_WorkoutPrefs_SaveSignInRequired",
                    WorkoutPreferencesError.ServerUnavailable => "Profile_WorkoutPrefs_SaveServerUnavailable",
                    WorkoutPreferencesError.ConcurrentUpdate => "Profile_WorkoutPrefs_SaveConflict",
                    WorkoutPreferencesError.ExerciseCatalogChanged => "Profile_WorkoutPrefs_SaveCatalogChanged",
                    _ => "Profile_WorkoutPrefs_PlanUpdateFailed"
                };
                return;
            }

            settingsSaved = true;
            _savedPreferencesSnapshot = CapturePreferencesSnapshot();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _refreshNotifier.InvalidateWorkoutAndDashboard();
            StatusMessage = "Workout preferences saved.";
            await _navigateAfterSaveAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileWorkoutPreferencesViewModel] Save failed ({ex.GetType().Name}).");
            StatusMessage = !settingsSaved
                ? "Workout preferences could not be saved. Please try again."
                : "Profile_WorkoutPrefs_PlanSetupCouldNotOpen";
        }
        finally
        {
            IsUpdatingPlan = false;
            IsBusy = false;
            OnPropertyChanged(nameof(CanSave));
        }
    }

    private void ToggleWorkoutActivity(WorkoutActivityOptionItem? item)
    {
        if (item is null)
            return;

        StatusMessage = ProfileWorkoutActivityOptions.ToggleSelection(WorkoutActivityOptions, item);
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(HasUnsavedChanges));
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

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value) =>
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));

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
