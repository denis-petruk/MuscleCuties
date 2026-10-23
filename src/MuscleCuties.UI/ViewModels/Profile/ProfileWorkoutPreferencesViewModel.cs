using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileWorkoutPreferencesViewModel : ObservableObject
{
    private const int DefaultDaysPerWeek = 3;
    private const int MinDaysPerWeek = WorkoutPreferencesRule.MinimumDaysPerWeek;
    private const int MaxDaysPerWeek = WorkoutPreferencesRule.MaximumDaysPerWeek;

    private readonly Func<Task> _navigateAfterSaveAsync;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly IProfileWorkoutPreferencesUseCase _useCase;
    [ObservableProperty] private int _daysPerWeek = DefaultDaysPerWeek;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isUpdatingPlan;

    [ObservableProperty] private SelectionOption<Equipment>? _selectedEquipmentOption;

    [ObservableProperty]
    private StrengthTrainingStyle _selectedStrengthTrainingStyle = StrengthTrainingStyle.ComfortableModerate;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StrengthTrainingStyleOptionItem> _strengthTrainingStyleOptions = new();

    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();

    public ProfileWorkoutPreferencesViewModel(
        IProfileWorkoutPreferencesUseCase useCase,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateBackAsync,
        Func<Task> navigateAfterSaveAsync)
    {
        _useCase = useCase;
        _refreshNotifier = refreshNotifier;
        _navigateAfterSaveAsync = navigateAfterSaveAsync;
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        SelectStrengthTrainingStyleCommand =
            new RelayCommand<StrengthTrainingStyleOptionItem>(SelectStrengthTrainingStyle);
        WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(new HashSet<WorkoutActivityType>());
        StrengthTrainingStyleOptions = ProfileWorkoutActivityOptions.BuildStrengthStyles(SelectedStrengthTrainingStyle);
        SelectedEquipmentOption = EquipmentOptions.First(option => option.Value == Equipment.FullGym);
    }

    public IReadOnlyList<SelectionOption<Equipment>> EquipmentOptions { get; } =
    [
        new(Equipment.FullGym, "Profile_WorkoutPrefs_EquipmentFullGym"),
        new(Equipment.HomeDumbbellsBands, "Profile_WorkoutPrefs_EquipmentHome"),
        new(Equipment.Bodyweight, "Profile_WorkoutPrefs_EquipmentBodyweight")
    ];

    public bool IsStrengthStyleVisible => WorkoutActivityOptions.Any(option =>
        ProfileWorkoutActivityOptions.IsStrengthActivity(option.ActivityType) && option.IsSelected);

    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions =>
        ProfileWorkoutActivityOptions.BuildGroups(WorkoutActivityOptions);

    public string DaysPerWeekLabel => $"{DaysPerWeek} {(DaysPerWeek == 1 ? "day" : "days")} / week";

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in workout preferences XAML.")]
    public int MinimumDaysPerWeek => MinDaysPerWeek;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in workout preferences XAML.")]
    public int MaximumDaysPerWeek => MaxDaysPerWeek;
    public double SliderDaysPerWeek
    {
        get => DaysPerWeek;
        set
        {
            if (!double.IsFinite(value))
                return;

            DaysPerWeek = (int)Math.Round(Math.Clamp(value, MinDaysPerWeek, MaxDaysPerWeek),
                MidpointRounding.AwayFromZero);
            OnPropertyChanged();
        }
    }

    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    public RelayCommand<StrengthTrainingStyleOptionItem> SelectStrengthTrainingStyleCommand { get; }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var preferences = await _useCase.LoadAsync();
            var selectedTypes = preferences?.ActivityTypes.Select(value => (WorkoutActivityType)value).ToHashSet()
                                ?? new HashSet<WorkoutActivityType>();
            SelectedStrengthTrainingStyle = preferences is null
                ? StrengthTrainingStyle.ComfortableModerate
                : (StrengthTrainingStyle)preferences.StrengthStyle;
            DaysPerWeek = preferences is null
                ? DefaultDaysPerWeek
                : Math.Clamp(preferences.DaysPerWeek <= 0 ? DefaultDaysPerWeek : preferences.DaysPerWeek,
                    MinDaysPerWeek, MaxDaysPerWeek);
            WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(selectedTypes);
            StrengthTrainingStyleOptions =
                ProfileWorkoutActivityOptions.BuildStrengthStyles(SelectedStrengthTrainingStyle);
            var selectedEquipment = Enum.TryParse<Equipment>(preferences?.EquipmentLevel, true, out var equipment) &&
                                    Enum.IsDefined(equipment)
                ? equipment : Equipment.FullGym;
            SelectedEquipmentOption = EquipmentOptions.First(option => option.Value == selectedEquipment);
            OnPropertyChanged(nameof(IsStrengthStyleVisible));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        var settingsSaved = false;
        try
        {
            var selectedTypes = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => option.ActivityType)
                .ToList();

            if (!selectedTypes.Any(ProfileWorkoutActivityOptions.IsStrengthActivity))
            {
                StatusMessage = "Choose one strength style so your plan stays personal.";
                return;
            }

            if (SelectedEquipmentOption is null)
            {
                StatusMessage = "Profile_WorkoutPrefs_ChooseEquipment";
                return;
            }

            IsUpdatingPlan = true;
            var result = await _useCase.SaveAndUpdatePlanAsync(new WorkoutPreferencesDto(
                selectedTypes.Select(value => (int)value).ToArray(),
                (int)SelectedStrengthTrainingStyle,
                DaysPerWeek,
                SelectedEquipmentOption.Value.ToString()));
            if (result.Error == WorkoutPreferencesError.ProfileMissing)
            {
                StatusMessage = "Complete personal info before workout preferences.";
                return;
            }

            if (result.Error == WorkoutPreferencesError.StrengthActivityRequired)
            {
                StatusMessage = "Choose one strength style so your plan stays personal.";
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
        }
    }

    private void ToggleWorkoutActivity(WorkoutActivityOptionItem? item)
    {
        if (item is null)
            return;

        StatusMessage = ProfileWorkoutActivityOptions.ToggleSelection(WorkoutActivityOptions, item);
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(IsStrengthStyleVisible));
    }

    private void SelectStrengthTrainingStyle(StrengthTrainingStyleOptionItem? item)
    {
        if (item is null)
            return;

        SelectedStrengthTrainingStyle = item.Style;
        foreach (var option in StrengthTrainingStyleOptions)
            option.IsSelected = option.Style == item.Style;

        StatusMessage = string.Empty;
    }

    partial void OnDaysPerWeekChanged(int value)
    {
        OnPropertyChanged(nameof(DaysPerWeekLabel));
        OnPropertyChanged(nameof(SliderDaysPerWeek));
    }

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(IsStrengthStyleVisible));
    }

    partial void OnSelectedStrengthTrainingStyleChanged(StrengthTrainingStyle value)
    {
        foreach (var option in StrengthTrainingStyleOptions)
            option.IsSelected = option.Style == value;
    }
}
