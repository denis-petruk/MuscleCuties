using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

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

    public DateTime MinBirthDate { get; } = DateTime.Today.AddYears(-100);
    public DateTime MaxBirthDate { get; } = DateTime.Today.AddYears(-12);

    public List<int> MetricHeightOptions { get; } = Enumerable.Range(100, 121).ToList();
    public List<int> FeetOptions { get; } = Enumerable.Range(4, 4).ToList();
    public List<int> InchesOptions { get; } = Enumerable.Range(0, 12).ToList();
    public List<int> MetricWeightOptions { get; } = Enumerable.Range(30, 171).ToList();
    public List<int> ImperialWeightOptions { get; } = Enumerable.Range(66, 375).ToList();

    public string WeightUnit => UseMetricSystem ? "kg" : "lbs";

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

            float heightCm = UseMetricSystem
                ? SelectedHeightCm
                : (SelectedFeet * 12 + SelectedInches) * 2.54f;

            float weightKg = UseMetricSystem
                ? SelectedWeightKg
                : SelectedWeightLbs * 0.453592f;

            var profile = new UserProfile
            {
                UserId = userId,
                Name = Name,
                DateOfBirth = BirthDate,
                Height = heightCm,
                Weight = weightKg,
                Goal = Goal,
                WorkoutDaysPerWeek = WorkoutDaysPerWeek,
                CycleLength = CycleLength
            };

            await _userRepository.AddProfileAsync(profile);
            _navigateToDashboard();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
