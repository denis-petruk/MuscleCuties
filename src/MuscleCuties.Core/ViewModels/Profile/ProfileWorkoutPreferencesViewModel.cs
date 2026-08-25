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
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string SelectedWorkoutActivitiesText
    {
        get
        {
            var selected = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Title)
                .ToList();

            if (selected.Count == 0)
                return "Pick at least one favorite way to move.";

            var preview = selected.Take(3).ToList();
            var suffix = selected.Count > preview.Count ? $" +{selected.Count - preview.Count}" : string.Empty;
            return $"{string.Join(", ", preview)}{suffix}";
        }
    }

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }

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
        WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());
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
            WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(selectedTypes);
            OnPropertyChanged(nameof(SelectedWorkoutActivitiesText));
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

            if (selectedTypes.Count == 0)
            {
                StatusMessage = "Choose at least one activity so your plan stays personal.";
                return;
            }

            var userId = await _authService.GetCurrentUserIdAsync();
            var profile = await _userRepository.GetProfileAsync(userId);
            if (profile is null)
            {
                StatusMessage = "Complete personal info before workout preferences.";
                return;
            }

            profile.PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(selectedTypes);
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

        item.IsSelected = !item.IsSelected;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedWorkoutActivitiesText));
    }

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        OnPropertyChanged(nameof(SelectedWorkoutActivitiesText));
    }
}
