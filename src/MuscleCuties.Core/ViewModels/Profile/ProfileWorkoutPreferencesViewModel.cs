using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileWorkoutPreferencesViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly ICycleService _cycleService;
    private readonly IWorkoutService _workoutService;
    private readonly Action _navigateBack;

    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();
    [ObservableProperty] private ObservableCollection<StrengthTrainingStyleOptionItem> _strengthTrainingStyleOptions = new();
    [ObservableProperty] private StrengthTrainingStyle _selectedStrengthTrainingStyle = StrengthTrainingStyle.ComfortableModerate;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public bool IsStrengthStyleVisible => WorkoutActivityOptions.Any(option =>
        WorkoutActivityPreferences.IsStrengthActivity(option.ActivityType) && option.IsSelected);
    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions =>
        WorkoutActivityOptionCatalog.BuildGroups(WorkoutActivityOptions);

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    public RelayCommand<StrengthTrainingStyleOptionItem> SelectStrengthTrainingStyleCommand { get; }

    public ProfileWorkoutPreferencesViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        ICycleService cycleService,
        IWorkoutService workoutService,
        Action navigateBack)
    {
        _authService = authService;
        _userRepository = userRepository;
        _cycleService = cycleService;
        _workoutService = workoutService;
        _navigateBack = navigateBack;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new RelayCommand(_navigateBack);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        SelectStrengthTrainingStyleCommand = new RelayCommand<StrengthTrainingStyleOptionItem>(SelectStrengthTrainingStyle);
        WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());
        StrengthTrainingStyleOptions = StrengthTrainingStyleOptionCatalog.Build(SelectedStrengthTrainingStyle);
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var profile = await _userRepository.GetProfileAsync(userId);
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
            var profile = await _userRepository.GetProfileAsync(userId);
            if (profile is null)
            {
                StatusMessage = "Complete personal info before workout preferences.";
                return;
            }

            profile.PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
                selectedTypes,
                SelectedStrengthTrainingStyle);
            profile.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateProfileAsync(profile);
            await _userRepository.AddSnapshotAsync(new UserProfileSnapshot
            {
                UserId = userId,
                SnapshotReason = "WorkoutPreferencesUpdate",
                ProfileJson = JsonSerializer.Serialize(new
                {
                    profile.PreferredWorkoutActivityTypes
                }),
                CreatedAt = DateTime.UtcNow
            });

            var phase = await _cycleService.GetCurrentPhaseAsync(userId);
            await _workoutService.RegenerateActivePlanAsync(userId, phase);
            StatusMessage = "Workout preferences saved.";
            _navigateBack();
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
