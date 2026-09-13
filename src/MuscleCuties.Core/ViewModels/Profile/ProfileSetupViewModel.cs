using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Common;
using MuscleCuties.Core.Models.UI.Profile;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileSetupViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly Func<Task> _navigateToQuizAsync;
    private readonly IQuizService _quizService;
    private readonly QuizQuestionCache _quizQuestionCache;
    private readonly IUserRepository _userRepository;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-25);
    [ObservableProperty] private int _cycleLength = 28;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private UserGoal _goal = UserGoal.MaintainHealth;
    private bool _hasLoadedProfile;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _profileImagePath = string.Empty;
    [ObservableProperty] private int _selectedFeet = 5;
    [ObservableProperty] private SelectionOption<UserGoal>? _selectedGoalOption;

    [ObservableProperty] private int _selectedHeightCm = 165;
    [ObservableProperty] private int _selectedInches = 6;

    [ObservableProperty]
    private StrengthTrainingStyle _selectedStrengthTrainingStyle = StrengthTrainingStyle.ComfortableModerate;

    [ObservableProperty] private int _selectedWeightKg = 65;
    [ObservableProperty] private int _selectedWeightLbs = 143;

    [ObservableProperty]
    private ObservableCollection<StrengthTrainingStyleOptionItem> _strengthTrainingStyleOptions = new();

    [ObservableProperty] private bool _useMetricSystem = true;
    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();
    [ObservableProperty] private int _workoutDaysPerWeek = 3;

    public ProfileSetupViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        IQuizService quizService,
        QuizQuestionCache quizQuestionCache,
        Func<Task> navigateToQuizAsync)
    {
        _authService = authService;
        _userRepository = userRepository;
        _quizService = quizService;
        _quizQuestionCache = quizQuestionCache;
        _navigateToQuizAsync = navigateToQuizAsync;
        ContinueCommand = new AsyncRelayCommand(ContinueAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SelectMetricUnitsCommand = new RelayCommand(() => UseMetricSystem = true);
        SelectImperialUnitsCommand = new RelayCommand(() => UseMetricSystem = false);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        SelectStrengthTrainingStyleCommand =
            new RelayCommand<StrengthTrainingStyleOptionItem>(SelectStrengthTrainingStyle);
        SelectedGoalOption = GoalOptions.First(option => option.Value == Goal);
        WorkoutActivityOptions = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());
        StrengthTrainingStyleOptions = StrengthTrainingStyleOptionCatalog.Build(SelectedStrengthTrainingStyle);
    }

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-12);

    public List<int> MetricHeightOptions { get; } = Enumerable.Range(100, 121).ToList();
    public List<int> FeetOptions { get; } = Enumerable.Range(4, 4).ToList();
    public List<int> InchesOptions { get; } = Enumerable.Range(0, 12).ToList();
    public List<int> MetricWeightOptions { get; } = Enumerable.Range(30, 171).ToList();
    public List<int> ImperialWeightOptions { get; } = Enumerable.Range(66, 375).ToList();
    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } = ProfileSelectionOptions.Goals;
    public bool UseImperialSystem => !UseMetricSystem;

    public bool IsStrengthStyleVisible => WorkoutActivityOptions.Any(option =>
        WorkoutActivityPreferences.IsStrengthActivity(option.ActivityType) && option.IsSelected);

    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions { get; private set; } = [];

    public string WeightUnit => UseMetricSystem ? "kg" : "lbs";
    public bool HasProfileImage => IsExistingProfileImage(ProfileImagePath);
    public bool HasNoProfileImage => !HasProfileImage;
    public string ProfileImageSource => HasProfileImage ? ProfileImagePath : string.Empty;

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
    public RelayCommand SelectMetricUnitsCommand { get; }
    public RelayCommand SelectImperialUnitsCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }
    public RelayCommand<StrengthTrainingStyleOptionItem> SelectStrengthTrainingStyleCommand { get; }

    public AsyncRelayCommand SaveCommand => ContinueCommand;

    private async Task LoadDataAsync()
    {
        var profile = await DataLoadScheduler.RunAsync(async () =>
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            return await _userRepository.GetProfileAsync(userId);
        });
        if (profile is null)
            return;

        Name = profile.Name;
        BirthDate = profile.DateOfBirth == default ? BirthDate : profile.DateOfBirth;
        Goal = profile.Goal;
        ProfileImagePath = profile.ProfileImagePath;
        SelectedGoalOption = GoalOptions.FirstOrDefault(option => option.Value == Goal)
                             ?? GoalOptions.First(option => option.Value == UserGoal.MaintainHealth);
        var selectedActivities = WorkoutActivityPreferences.Parse(profile.PreferredWorkoutActivityTypes);
        var loadedOptions = WorkoutActivityOptionCatalog.Build(selectedActivities);
        foreach (var option in WorkoutActivityOptions)
            option.IsSelected = loadedOptions.Any(loaded => loaded.ActivityType == option.ActivityType && loaded.IsSelected);
        SelectedStrengthTrainingStyle =
            WorkoutActivityPreferences.ParseStrengthStyle(profile.PreferredWorkoutActivityTypes);
        OnPropertyChanged(nameof(IsStrengthStyleVisible));
        _hasLoadedProfile = true;
    }

    partial void OnUseMetricSystemChanged(bool value)
    {
        OnPropertyChanged(nameof(WeightUnit));
        OnPropertyChanged(nameof(UseImperialSystem));
    }

    private async Task ContinueAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var userId = await DataLoadScheduler.RunAsync(_authService.GetCurrentUserIdAsync);
            var selectedActivities = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => option.ActivityType)
                .ToList();

            if (!selectedActivities.Any(WorkoutActivityPreferences.IsStrengthActivity))
            {
                ErrorMessage = "Pick one strength style so your plan has a real base.";
                return;
            }

            selectedActivities = WorkoutActivityPreferences.EnsureRequired(selectedActivities).ToList();

            var heightCm = UseMetricSystem
                ? SelectedHeightCm
                : (SelectedFeet * 12 + SelectedInches) * 2.54f;

            var weightKg = UseMetricSystem
                ? SelectedWeightKg
                : SelectedWeightLbs * 0.453592f;

            var profile = await DataLoadScheduler.RunAsync(() => _userRepository.GetProfileAsync(userId));
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
                : CycleLength > 0
                    ? CycleLength
                    : 28;
            profile.UpdatedAt = DateTime.UtcNow;

            if (isNewProfile)
                await DataLoadScheduler.RunAsync(() => _userRepository.AddProfileAsync(profile));
            else
                await DataLoadScheduler.RunAsync(() => _userRepository.UpdateProfileAsync(profile));

            await DataLoadScheduler.RunAsync(() => _userRepository.AddSnapshotAsync(new UserProfileSnapshot
            {
                UserId = userId,
                SnapshotReason = isNewProfile ? "InitialProfileSetup" : "ProfileSetup",
                ProfileJson = JsonSerializer.Serialize(new
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
            }));

            var user = await DataLoadScheduler.RunAsync(() => _userRepository.GetByIdAsync(userId));
            if (user is not null)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await DataLoadScheduler.RunAsync(() => _userRepository.UpdateAsync(user));
            }

            await _quizQuestionCache.GetOrLoadAsync(() =>
                DataLoadScheduler.RunAsync(_quizService.GetOnboardingQuestionsAsync));
            await _navigateToQuizAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "We could not finish setup. Please try again.";
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

        ErrorMessage = WorkoutActivityOptionCatalog.ToggleSelection(WorkoutActivityOptions, item);
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
        GroupedWorkoutActivityOptions = WorkoutActivityOptionCatalog.BuildGroups(value);
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
        OnPropertyChanged(nameof(IsStrengthStyleVisible));
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

    private static bool IsExistingProfileImage(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
