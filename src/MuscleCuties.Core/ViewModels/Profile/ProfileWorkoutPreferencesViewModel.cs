using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Profile;
using Microsoft.Extensions.DependencyInjection;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileWorkoutPreferencesViewModel : ObservableObject
{
    private readonly IAppPreloadService _preloadService;
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task> _navigateAfterSaveAsync;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkoutService _workoutService;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty]
    private StrengthTrainingStyle _selectedStrengthTrainingStyle = StrengthTrainingStyle.ComfortableModerate;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StrengthTrainingStyleOptionItem> _strengthTrainingStyleOptions = new();

    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();

    public ProfileWorkoutPreferencesViewModel(
        IAuthService authService,
        IServiceScopeFactory scopeFactory,
        ICycleService cycleService,
        IWorkoutService workoutService,
        IAppPreloadService preloadService,
        Func<Task> navigateBackAsync,
        Func<Task> navigateAfterSaveAsync)
    {
        _authService = authService;
        _scopeFactory = scopeFactory;
        _cycleService = cycleService;
        _workoutService = workoutService;
        _preloadService = preloadService;
        _navigateBackAsync = navigateBackAsync;
        _navigateAfterSaveAsync = navigateAfterSaveAsync;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new AsyncRelayCommand(_navigateBackAsync);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        SelectStrengthTrainingStyleCommand =
            new RelayCommand<StrengthTrainingStyleOptionItem>(SelectStrengthTrainingStyle);
        WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());
        StrengthTrainingStyleOptions = StrengthTrainingStyleOptionCatalog.Build(SelectedStrengthTrainingStyle);
    }

    public bool IsStrengthStyleVisible => WorkoutActivityOptions.Any(option =>
        WorkoutActivityPreferences.IsStrengthActivity(option.ActivityType) && option.IsSelected);

    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions =>
        WorkoutActivityOptionCatalog.BuildGroups(WorkoutActivityOptions);

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    public RelayCommand<StrengthTrainingStyleOptionItem> SelectStrengthTrainingStyleCommand { get; }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            await using var scope = _scopeFactory.CreateAsyncScope();
            var profile = await scope.ServiceProvider.GetRequiredService<IProfileSettingsService>()
                .GetProfileAsync(userId);
            var selectedTypes = WorkoutActivityPreferences.Parse(profile?.PreferredWorkoutActivityTypes);
            SelectedStrengthTrainingStyle =
                WorkoutActivityPreferences.ParseStrengthStyle(profile?.PreferredWorkoutActivityTypes);
            WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(selectedTypes);
            StrengthTrainingStyleOptions =
                StrengthTrainingStyleOptionCatalog.Build(SelectedStrengthTrainingStyle);
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
        var planUpdated = false;
        try
        {
            var selectedTypes = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => option.ActivityType)
                .ToList();

            if (!selectedTypes.Any(WorkoutActivityPreferences.IsStrengthActivity))
            {
                StatusMessage = "Choose one strength style so your plan stays personal.";
                return;
            }

            selectedTypes = WorkoutActivityPreferences.EnsureRequired(selectedTypes).ToList();
            var userId = await _authService.GetCurrentUserIdAsync();
            var serialized = WorkoutActivityPreferences.Serialize(
                selectedTypes,
                SelectedStrengthTrainingStyle);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var saved = await scope.ServiceProvider.GetRequiredService<IProfileSettingsService>()
                .SaveWorkoutPreferencesAsync(userId, serialized);
            if (!saved)
            {
                StatusMessage = "Complete personal info before workout preferences.";
                return;
            }

            settingsSaved = true;
            var phase = await _cycleService.GetCurrentPhaseAsync(userId);
            await _workoutService.RegenerateActivePlanAsync(userId, phase);
            planUpdated = true;
            _preloadService.InvalidateWorkout();
            _preloadService.InvalidateDashboard();
            StatusMessage = "Workout preferences saved.";
            await _navigateAfterSaveAsync();
        }
        catch (Exception)
        {
            StatusMessage = !settingsSaved
                ? "Workout preferences could not be saved. Please try again."
                : !planUpdated
                    ? "Preferences saved, but the plan could not finish updating. Please try again."
                    : "Plan updated, but Workout could not open. Use the Train tab to continue.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleWorkoutActivity(WorkoutActivityOptionItem? item)
    {
        if (item is null)
            return;

        StatusMessage = WorkoutActivityOptionCatalog.ToggleSelection(WorkoutActivityOptions, item);
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
