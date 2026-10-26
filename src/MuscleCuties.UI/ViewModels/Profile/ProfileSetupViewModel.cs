using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileSetupViewModel : ObservableObject
{
    private const int InchesPerFoot = 12;
    private const float CentimetersPerInch = 2.54f;
    private const float KilogramsPerPound = 0.453592f;

    private readonly IProfileHealthConnection _healthConnection;
    private readonly Func<Task> _navigateToQuizAsync;
    private readonly IProfileSetupUseCase _useCase;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-18);
    private bool _birthDateWasSelected;
    private bool _loadingBirthDate;
    [ObservableProperty] private int _cycleLength = 28;
    [ObservableProperty] private string _errorMessage = string.Empty;
    private bool _hasLoadedProfile;
    private bool _isPendingRegistration;
    [ObservableProperty] private string _healthConnectionMessage = string.Empty;
    [ObservableProperty] private string _healthConnectionText = "Tap to Connect";
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _profileImagePath = string.Empty;
    [ObservableProperty] private int _selectedFeet = 5;
    [ObservableProperty] private SelectionOption<UserGoal>? _selectedGoalOption;

    [ObservableProperty] private int _selectedHeightCm = 165;
    [ObservableProperty] private int _selectedInches = 6;

    [ObservableProperty] private int _selectedWeightKg = 65;
    [ObservableProperty] private int _selectedWeightLbs = 143;

    [ObservableProperty] private bool _useMetricSystem = true;
    [ObservableProperty] private ObservableCollection<WorkoutActivityOptionItem> _workoutActivityOptions = new();

    public ProfileSetupViewModel(
        IProfileSetupUseCase useCase,
        IProfileHealthConnection healthConnection,
        Func<Task> navigateToQuizAsync)
    {
        _useCase = useCase;
        _healthConnection = healthConnection;
        _navigateToQuizAsync = navigateToQuizAsync;
        ContinueCommand = new AsyncRelayCommand(ContinueAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        ConnectHealthCommand = new AsyncRelayCommand(ConnectHealthAsync);
        SelectMetricUnitsCommand = new RelayCommand(() => UseMetricSystem = true);
        SelectImperialUnitsCommand = new RelayCommand(() => UseMetricSystem = false);
        ToggleWorkoutActivityCommand = new RelayCommand<WorkoutActivityOptionItem>(ToggleWorkoutActivity);
        WorkoutActivityOptions = ProfileWorkoutActivityOptions.Build(new HashSet<WorkoutActivityDto>());
    }

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-18);

    public List<int> MetricHeightOptions { get; } = Enumerable.Range(100, 121).ToList();
    public List<int> FeetOptions { get; } = Enumerable.Range(4, 4).ToList();
    public List<int> InchesOptions { get; } = Enumerable.Range(0, 12).ToList();
    public List<int> MetricWeightOptions { get; } = Enumerable.Range(30, 171).ToList();
    public List<int> ImperialWeightOptions { get; } = Enumerable.Range(66, 375).ToList();

    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } =
    [
        new(UserGoal.FatLoss, "Fat Loss"),
        new(UserGoal.MuscleTone, "Muscle Tone"),
        new(UserGoal.Strength, "Strength"),
        new(UserGoal.MaintainHealth, "Maintain Health")
    ];

    public bool UseImperialSystem => !UseMetricSystem;
    public bool IsAppleHealthAvailable => !_isPendingRegistration && _healthConnection.IsAvailable;
    public AsyncRelayCommand ConnectHealthCommand { get; }

    public IReadOnlyList<WorkoutActivityGroupSection> GroupedWorkoutActivityOptions { get; private set; } = [];

    public string WeightUnit => UseMetricSystem ? "kg" : "lbs";
    public bool HasProfileImage => !string.IsNullOrWhiteSpace(ProfileImagePath) && File.Exists(ProfileImagePath);
    public bool HasNoProfileImage => !HasProfileImage;
    public string ProfileImageSource => HasProfileImage ? ProfileImagePath : string.Empty;

    public float Height
    {
        get => UseMetricSystem ? SelectedHeightCm : (SelectedFeet * InchesPerFoot + SelectedInches) * CentimetersPerInch;
        set => SelectedHeightCm = (int)value;
    }

    public float Weight
    {
        get => UseMetricSystem ? SelectedWeightKg : SelectedWeightLbs * KilogramsPerPound;
        set => SelectedWeightKg = (int)value;
    }

    public AsyncRelayCommand ContinueCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public RelayCommand SelectMetricUnitsCommand { get; }
    public RelayCommand SelectImperialUnitsCommand { get; }
    public RelayCommand<WorkoutActivityOptionItem> ToggleWorkoutActivityCommand { get; }

    public AsyncRelayCommand SaveCommand => ContinueCommand;

    private async Task LoadDataAsync()
    {
        var result = await _useCase.LoadAsync();
        _isPendingRegistration = result.HasPendingRegistration;
        OnPropertyChanged(nameof(IsAppleHealthAvailable));
        if (result.HasPendingRegistration)
        {
            if (result.Profile is { } staged)
            {
                Name = staged.Name;
                ApplyLoadedBirthDate(staged.BirthDate);
                ProfileImagePath = staged.ProfileImagePath;
                CycleLength = staged.CycleLength;
                UseMetricSystem = staged.UseMetricSystem;
                SelectedHeightCm = (int)staged.HeightCm;
                SelectedWeightKg = (int)staged.WeightKg;
                var totalInches = (int)Math.Round(staged.HeightCm / CentimetersPerInch);
                SelectedFeet = totalInches / InchesPerFoot;
                SelectedInches = totalInches % InchesPerFoot;
                SelectedWeightLbs = (int)Math.Round(staged.WeightKg / KilogramsPerPound);

                var selectedOptions = ProfileWorkoutActivityOptions.Build(
                    staged.SelectedActivityTypes.Select(value => (WorkoutActivityDto)value).ToHashSet());
                foreach (var option in WorkoutActivityOptions)
                    option.IsSelected = selectedOptions.Any(s => s.ActivityType == option.ActivityType && s.IsSelected);
            }
            else if (!string.IsNullOrWhiteSpace(result.PendingUsername))
            {
                Name = result.PendingUsername;
            }

            return;
        }

        if (IsAppleHealthAvailable)
            HealthConnectionText = await _healthConnection.IsConnectedAsync()
                ? "Connected"
                : "Tap to Connect";
        var profile = result.Profile;
        if (profile is null)
            return;

        Name = profile.Name;
        ApplyLoadedBirthDate(profile.BirthDate);
        ProfileImagePath = profile.ProfileImagePath;
        var selectedActivities = profile.SelectedActivityTypes
            .Select(value => (WorkoutActivityDto)value).ToHashSet();
        var loadedOptions = ProfileWorkoutActivityOptions.Build(selectedActivities);
        foreach (var option in WorkoutActivityOptions)
            option.IsSelected =
                loadedOptions.Any(loaded => loaded.ActivityType == option.ActivityType && loaded.IsSelected);
        _hasLoadedProfile = true;
    }

    private async Task ConnectHealthAsync()
    {
        if (!IsAppleHealthAvailable)
            return;

        var result = await _healthConnection.ConnectAsync();
        HealthConnectionText = result.IsConnected ? "Connected" : "Tap to Connect";
        HealthConnectionMessage = result.Message;
    }

    partial void OnUseMetricSystemChanged(bool value)
    {
        OnPropertyChanged(nameof(WeightUnit));
        OnPropertyChanged(nameof(UseImperialSystem));
    }

    partial void OnBirthDateChanged(DateTime value)
    {
        if (!_loadingBirthDate)
            _birthDateWasSelected = true;
        var today = DateTime.Today;
        var age = today.Year - value.Year;
        if (value.Date > today.AddYears(-age))
            age--;

        if (age < 18)
            ErrorMessage = "Please provide a valid date of birth (must be at least 18 years old).";
        else if (ErrorMessage == "Please provide a valid date of birth (must be at least 18 years old).")
            ErrorMessage = string.Empty;
    }

    public void MarkBirthDateSelected()
    {
        if (!_loadingBirthDate)
            _birthDateWasSelected = true;
    }

    private void ApplyLoadedBirthDate(DateTime birthDate)
    {
        _loadingBirthDate = true;
        try
        {
            if (birthDate != default)
                BirthDate = birthDate;
            _birthDateWasSelected = birthDate != default;
        }
        finally
        {
            _loadingBirthDate = false;
        }
    }

    private async Task ContinueAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (!_birthDateWasSelected || BirthDate == default)
            {
                ErrorMessage = "Please provide your date of birth.";
                return;
            }

            var selectedActivities = WorkoutActivityOptions
                .Where(option => option.IsSelected)
                .Select(option => (int)option.ActivityType)
                .ToArray();

            if (!WorkoutActivityOptions.Any(option => option.IsSelected && WorkoutPlanningConstants.IsStrengthActivity(option.ActivityType)))
            {
                ErrorMessage = "At least one strength activity is required for your plan.";
                return;
            }

            var heightCm = UseMetricSystem
                ? SelectedHeightCm
                : (SelectedFeet * InchesPerFoot + SelectedInches) * CentimetersPerInch;

            var weightKg = UseMetricSystem
                ? SelectedWeightKg
                : SelectedWeightLbs * KilogramsPerPound;

            var result = await _useCase.SaveAndPrepareQuizAsync(new ProfileSetupDraft(
                Name, BirthDate, heightCm, weightKg, ProfileImagePath,
                CycleLength, selectedActivities, UseMetricSystem,
                _hasLoadedProfile));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ??
                    (result.Error == ProfileSetupError.AccountMissing
                        ? "Active account not found. Please try again."
                        : "Check your profile details and try again.");
                return;
            }

            await _navigateToQuizAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileSetupViewModel] Setup failed ({ex.GetType().Name}).");
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

        ErrorMessage = ProfileWorkoutActivityOptions.ToggleSelection(WorkoutActivityOptions, item);
    }

    public void SetProfileImage(string? imagePath)
    {
        ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        ErrorMessage = string.Empty;
    }

    partial void OnWorkoutActivityOptionsChanged(ObservableCollection<WorkoutActivityOptionItem> value)
    {
        GroupedWorkoutActivityOptions = ProfileWorkoutActivityOptions.BuildGroups(value);
        OnPropertyChanged(nameof(GroupedWorkoutActivityOptions));
    }


    partial void OnProfileImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasProfileImage));
        OnPropertyChanged(nameof(HasNoProfileImage));
        OnPropertyChanged(nameof(ProfileImageSource));
    }
}
