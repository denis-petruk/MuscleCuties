using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileWorkoutPreferencesViewModel : ObservableObject
{
    private const string FullGymEquipmentValue = "FullGym";
    private const string HomeDumbbellsBandsEquipmentValue = "HomeDumbbellsBands";
    private const string BodyweightEquipmentValue = "Bodyweight";

    private readonly Func<Task> _navigateAfterSaveAsync;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly IProfileWorkoutPreferencesUseCase _useCase;
    private readonly GetReviewedExerciseExclusionCatalogV020UseCase _exerciseExclusionCatalog;

    [ObservableProperty] private bool _isBusy;
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
    public IReadOnlyList<WorkoutExerciseExclusionOptionItem> FilteredExerciseExclusionOptions =>
        string.IsNullOrWhiteSpace(ExerciseExclusionSearchText)
            ? ExerciseExclusionOptions.ToArray()
            : ExerciseExclusionOptions.Where(option =>
                    option.Name.Contains(ExerciseExclusionSearchText.Trim(), StringComparison.CurrentCultureIgnoreCase))
                .ToArray();
    public bool CanSave => !IsBusy && IsExerciseExclusionCatalogLoaded;

    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    public RelayCommand<WorkoutWeekdayOptionItem> ToggleWeekdayCommand { get; }

    private async Task LoadDataAsync()
    {
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
            if (preferences is not null)
            {
                foreach (var option in WorkoutActivityOptions)
                {
                    if (preferences.ActivitySessionDurationsMinutes.TryGetValue(
                            (int)option.ActivityType, out var durationMinutes))
                        option.SessionDurationMinutes = durationMinutes;
                    if (preferences.RequestedSessionsPerWeek?.TryGetValue(
                            (int)option.ActivityType, out var sessionsPerWeek) == true)
                        option.RequestedSessionsPerWeek = sessionsPerWeek;
                }
            }

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
            IsExerciseExclusionCatalogLoaded = true;
            OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
            OnPropertyChanged(nameof(SelectedWeekdayCount));
            OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[ProfileWorkoutPreferencesViewModel] Catalog load failed ({exception.GetType().Name}).");
            IsExerciseExclusionCatalogLoaded = false;
            StatusMessage = "Profile_WorkoutPrefs_ExerciseCatalogUnavailable";
        }
        finally
        {
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

            if (selectedActivities.Any(option =>
                    WorkoutPreferencesConstraints.ActivityRequiresSessionDuration(option.ActivityType) &&
                    (option.SessionDurationMinutes is null ||
                     !WorkoutPreferencesConstraints.IsValidSessionDuration(option.ActivityType,
                         option.SessionDurationMinutes.Value))))
            {
                StatusMessage = "Profile_WorkoutPrefs_ChooseActivitySessionLength";
                return;
            }

            if (selectedActivities.Any(option =>
                    WorkoutPreferencesConstraints.ActivityRequiresExplicitWeeklyFrequency(option.ActivityType) &&
                    (option.RequestedSessionsPerWeek is null ||
                     !WorkoutPreferencesConstraints.IsValidRequestedWeeklyFrequency(
                         option.RequestedSessionsPerWeek.Value))))
            {
                StatusMessage = "Profile_WorkoutPrefs_ChooseActivityWeeklyFrequency";
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
                        option => option.SessionDurationMinutes!.Value),
                SelectedEquipmentOption.Value,
                selectedActivities
                    .Where(option => WorkoutPreferencesConstraints.ActivityRequiresExplicitWeeklyFrequency(
                        option.ActivityType))
                    .ToDictionary(option => (int)option.ActivityType,
                        option => option.RequestedSessionsPerWeek!.Value),
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

            settingsSaved = true;
            _refreshNotifier.InvalidateWorkoutAndDashboard();
            StatusMessage = "Workout preferences saved.";
            await _navigateAfterSaveAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileWorkoutPreferencesViewModel] Save failed ({ex.GetType().Name}).");
            StatusMessage = !settingsSaved
                ? "Workout preferences could not be saved. Please try again."
                : "Plan updated, but Workout could not open. Use the Train tab to continue.";
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
    }

    private void ToggleWeekday(WorkoutWeekdayOptionItem? item)
    {
        if (item is null)
            return;

        item.IsSelected = !item.IsSelected;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedWeekdayCount));
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

    partial void OnExerciseExclusionOptionsChanged(ObservableCollection<WorkoutExerciseExclusionOptionItem> value) =>
        OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));

    partial void OnExerciseExclusionSearchTextChanged(string value) =>
        OnPropertyChanged(nameof(FilteredExerciseExclusionOptions));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    partial void OnIsExerciseExclusionCatalogLoadedChanged(bool value) => OnPropertyChanged(nameof(CanSave));
}
