using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Common;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileSetupViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly IHealthSyncService? _healthSyncService;
    private readonly Action _navigateToDashboard;
    private bool _hasLoadedProfile;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-25);
    [ObservableProperty] private bool _useMetricSystem = true;
    [ObservableProperty] private UserGoal _goal = UserGoal.MaintainHealth;
    [ObservableProperty] private SelectionOption<UserGoal>? _selectedGoalOption;
    [ObservableProperty] private int _workoutDaysPerWeek = 3;
    [ObservableProperty] private int _cycleLength = 28;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isHealthSyncBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _healthSyncStatusText = "Not connected";
    [ObservableProperty] private string _healthSyncMessage = string.Empty;
    [ObservableProperty] private string _profileImagePath = string.Empty;

    [ObservableProperty] private int _selectedHeightCm = 165;
    [ObservableProperty] private int _selectedFeet = 5;
    [ObservableProperty] private int _selectedInches = 6;
    [ObservableProperty] private int _selectedWeightKg = 65;
    [ObservableProperty] private int _selectedWeightLbs = 143;
    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();
    [ObservableProperty] private ObservableCollection<StrengthTrainingStyleOptionItem> _strengthTrainingStyleOptions = new();
    [ObservableProperty] private StrengthTrainingStyle _selectedStrengthTrainingStyle = StrengthTrainingStyle.ComfortableModerate;

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-12);

    public List<int> MetricHeightOptions { get; } = Enumerable.Range(100, 121).ToList();
    public List<int> FeetOptions { get; } = Enumerable.Range(4, 4).ToList();
    public List<int> InchesOptions { get; } = Enumerable.Range(0, 12).ToList();
    public List<int> MetricWeightOptions { get; } = Enumerable.Range(30, 171).ToList();
    public List<int> ImperialWeightOptions { get; } = Enumerable.Range(66, 375).ToList();
    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } = ProfileSelectionOptions.Goals;
    public bool IsStrengthStyleVisible => WorkoutActivityOptions.Any(option =>
        IsStrengthActivity(option.ActivityType) && option.IsSelected);

    public string WeightUnit => UseMetricSystem ? "kg" : "lbs";
    public bool HasProfileImage => !string.IsNullOrWhiteSpace(ProfileImagePath);
    public bool HasNoProfileImage => !HasProfileImage;
    public string ProfileImageSource => ProfileImagePath;
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
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand ConnectAppleHealthCommand { get; }
    public AsyncRelayCommand ConnectWhoopCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    public RelayCommand<StrengthTrainingStyleOptionItem> SelectStrengthTrainingStyleCommand { get; }

    // Alias for tests that use SaveCommand
    public AsyncRelayCommand SaveCommand => ContinueCommand;

    public ProfileSetupViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        Action navigateToDashboard,
        IHealthSyncService? healthSyncService = null)
    {
        _authService = authService;
        _userRepository = userRepository;
        _healthSyncService = healthSyncService;
        _navigateToDashboard = navigateToDashboard;
        ContinueCommand = new AsyncRelayCommand(ContinueAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        ConnectAppleHealthCommand = new AsyncRelayCommand(() => ConnectHealthAsync(HealthDataSource.AppleHealth));
        ConnectWhoopCommand = new AsyncRelayCommand(() => ConnectHealthAsync(HealthDataSource.Whoop));
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        SelectStrengthTrainingStyleCommand = new RelayCommand<StrengthTrainingStyleOptionItem>(SelectStrengthTrainingStyle);
        SelectedGoalOption = GoalOptions.First(option => option.Value == Goal);
        WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());
        StrengthTrainingStyleOptions = StrengthTrainingStyleOptionCatalog.Build(SelectedStrengthTrainingStyle);
    }

    private async Task LoadDataAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile is not null)
        {
            Name = profile.Name;
            BirthDate = profile.DateOfBirth == default ? BirthDate : profile.DateOfBirth;
            Goal = profile.Goal;
            ProfileImagePath = profile.ProfileImagePath;
            SelectedGoalOption = GoalOptions.FirstOrDefault(option => option.Value == Goal)
                                 ?? GoalOptions.First(option => option.Value == UserGoal.MaintainHealth);
            WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(
                WorkoutActivityPreferences.Parse(profile.PreferredWorkoutActivityTypes));
            SelectedStrengthTrainingStyle =
                WorkoutActivityPreferences.ParseStrengthStyle(profile.PreferredWorkoutActivityTypes);
            StrengthTrainingStyleOptions =
                StrengthTrainingStyleOptionCatalog.Build(SelectedStrengthTrainingStyle);
            _hasLoadedProfile = true;
        }

        await RefreshHealthSyncStatusAsync(userId);
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
            profile.ProfileImagePath = ProfileImagePath.Trim();
            if (isNewProfile || _hasLoadedProfile)
            {
                profile.Goal = Goal;
                profile.WeightGoalPace = WeightGoalPace.Steady;
                profile.PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
                    selectedActivities,
                    SelectedStrengthTrainingStyle);
            }
            profile.WorkoutDaysPerWeek = profile.WorkoutDaysPerWeek > 0
                ? profile.WorkoutDaysPerWeek
                : WorkoutDaysPerWeek;
            profile.CycleLength = profile.CycleLength > 0
                ? profile.CycleLength
                : CycleLength > 0 ? CycleLength : 28;
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
                    profile.ProfileImagePath,
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
        OnPropertyChanged(nameof(IsStrengthStyleVisible));
    }

    private void SelectStrengthTrainingStyle(StrengthTrainingStyleOptionItem? item)
    {
        if (item is null)
            return;

        SelectedStrengthTrainingStyle = item.Style;
        foreach (var option in StrengthTrainingStyleOptions)
            option.IsSelected = option.Style == item.Style;

        ErrorMessage = string.Empty;
    }

    public void SetProfileImage(string? imagePath)
    {
        ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        ErrorMessage = string.Empty;
    }

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        OnPropertyChanged(nameof(SelectedWorkoutActivitiesText));
        OnPropertyChanged(nameof(IsStrengthStyleVisible));
    }

    private async Task ConnectHealthAsync(HealthDataSource source)
    {
        if (_healthSyncService is null)
        {
            HealthSyncMessage = "Health sync is not available in this build.";
            return;
        }

        IsHealthSyncBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var result = await _healthSyncService.SyncAsync(userId, source);
            HealthSyncMessage = result.Message;
            await RefreshHealthSyncStatusAsync(userId);
        }
        finally
        {
            IsHealthSyncBusy = false;
        }
    }

    private async Task RefreshHealthSyncStatusAsync(int userId)
    {
        if (_healthSyncService is null)
            return;

        var status = await _healthSyncService.GetStatusAsync(userId);
        HealthSyncStatusText = status.SummaryText;
    }

    partial void OnGoalChanged(UserGoal value)
    {
        var selected = GoalOptions.FirstOrDefault(option => option.Value == value);
        if (selected is not null && SelectedGoalOption?.Value != value)
            SelectedGoalOption = selected;
    }

    partial void OnSelectedGoalOptionChanged(SelectionOption<UserGoal>? value)
    {
        if (value is not null && Goal != value.Value)
            Goal = value.Value;
    }

    partial void OnSelectedStrengthTrainingStyleChanged(StrengthTrainingStyle value)
    {
        foreach (var option in StrengthTrainingStyleOptions)
            option.IsSelected = option.Style == value;
    }

    partial void OnProfileImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasProfileImage));
        OnPropertyChanged(nameof(HasNoProfileImage));
        OnPropertyChanged(nameof(ProfileImageSource));
    }

    private static bool IsStrengthActivity(WorkoutActivityType activityType) =>
        activityType is WorkoutActivityType.StrengthHighIntensity or
            WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.RockClimbing;
}
