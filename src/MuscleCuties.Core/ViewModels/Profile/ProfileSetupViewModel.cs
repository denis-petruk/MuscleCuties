using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileSetupViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly Action _navigateToDashboard;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-25);
    [ObservableProperty] private bool _useMetricSystem = true;
    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private int _workoutDaysPerWeek = 3;
    [ObservableProperty] private int _cycleLength = 28;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private int _selectedHeightCm = 165;
    [ObservableProperty] private int _selectedFeet = 5;
    [ObservableProperty] private int _selectedInches = 6;
    [ObservableProperty] private int _selectedWeightKg = 65;
    [ObservableProperty] private int _selectedWeightLbs = 143;
    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-12);

    public List<int> MetricHeightOptions { get; } = Enumerable.Range(100, 121).ToList();
    public List<int> FeetOptions { get; } = Enumerable.Range(4, 4).ToList();
    public List<int> InchesOptions { get; } = Enumerable.Range(0, 12).ToList();
    public List<int> MetricWeightOptions { get; } = Enumerable.Range(30, 171).ToList();
    public List<int> ImperialWeightOptions { get; } = Enumerable.Range(66, 375).ToList();

    public string WeightUnit => UseMetricSystem ? "kg" : "lbs";
    public string SelectedWorkoutActivitiesText
    {
        get
        {
            var selected = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Title)
                .ToList();

            if (selected.Count == 0)
                return "Pick the movement you actually like.";

            var preview = selected.Take(3).ToList();
            var suffix = selected.Count > preview.Count ? $" +{selected.Count - preview.Count}" : string.Empty;
            return $"{string.Join(", ", preview)}{suffix}";
        }
    }

    // Legacy property kept for test compatibility
    public float Height
    {
        get => UseMetricSystem ? SelectedHeightCm : (SelectedFeet * 12f + SelectedInches) * 2.54f;
        set => SelectedHeightCm = (int)value;
    }

    public float Weight
    {
        get => UseMetricSystem ? SelectedWeightKg : SelectedWeightLbs * 0.453592f;
        set => SelectedWeightKg = (int)value;
    }

    public AsyncRelayCommand ContinueCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }

    // Alias for tests that use SaveCommand
    public AsyncRelayCommand SaveCommand => ContinueCommand;

    public ProfileSetupViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        Action navigateToDashboard)
    {
        _authService = authService;
        _userRepository = userRepository;
        _navigateToDashboard = navigateToDashboard;
        ContinueCommand = new AsyncRelayCommand(ContinueAsync);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());
    }

    partial void OnUseMetricSystemChanged(bool value)
    {
        OnPropertyChanged(nameof(WeightUnit));
    }

    private async Task ContinueAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var selectedActivities = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => option.ActivityType)
                .ToList();

            if (selectedActivities.Count == 0)
            {
                ErrorMessage = "Pick at least one activity so your plan has a real direction.";
                return;
            }

            float heightCm = UseMetricSystem
                ? SelectedHeightCm
                : (SelectedFeet * 12 + SelectedInches) * 2.54f;

            float weightKg = UseMetricSystem
                ? SelectedWeightKg
                : SelectedWeightLbs * 0.453592f;

            var profile = await _userRepository.GetProfileAsync(userId);
            var isNewProfile = profile is null;

            profile ??= new UserProfile
            {
                UserId = userId,
                Goal = Goal,
                WorkoutDaysPerWeek = WorkoutDaysPerWeek,
                CycleLength = CycleLength,
                WeightGoalPace = WeightGoalPace.Steady
            };

            profile.Name = Name;
            profile.DateOfBirth = BirthDate;
            profile.Height = heightCm;
            profile.Weight = weightKg;
            profile.WorkoutDaysPerWeek = profile.WorkoutDaysPerWeek > 0
                ? profile.WorkoutDaysPerWeek
                : WorkoutDaysPerWeek;
            profile.CycleLength = profile.CycleLength > 0
                ? profile.CycleLength
                : CycleLength > 0 ? CycleLength : 28;
            profile.PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(selectedActivities);
            profile.UpdatedAt = DateTime.UtcNow;

            if (isNewProfile)
                await _userRepository.AddProfileAsync(profile);
            else
                await _userRepository.UpdateProfileAsync(profile);

            await _userRepository.AddSnapshotAsync(new UserProfileSnapshot
            {
                UserId = userId,
                SnapshotReason = isNewProfile ? "InitialProfileSetup" : "ProfileSetup",
                ProfileJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    profile.Name,
                    profile.DateOfBirth,
                    profile.Height,
                    profile.Weight,
                    Goal = profile.Goal.ToString(),
                    WeightGoalPace = profile.WeightGoalPace.ToString(),
                    TrainingExperienceLevel = profile.TrainingExperienceLevel.ToString(),
                    CycleTrackingMode = profile.CycleTrackingMode.ToString(),
                    profile.WorkoutDaysPerWeek,
                    profile.CycleLength,
                    profile.DietaryTags,
                    profile.PreferredWorkoutActivityTypes
                }),
                CreatedAt = DateTime.UtcNow
            });

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is not null)
            {
                user.IsOnboardingComplete = true;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
            }

            _navigateToDashboard();
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
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedWorkoutActivitiesText));
    }

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        OnPropertyChanged(nameof(SelectedWorkoutActivitiesText));
    }
}
