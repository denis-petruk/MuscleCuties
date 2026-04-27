using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly INutritionService _nutritionService;
    private readonly IWorkoutRepository _workoutRepository;
    private readonly Action _openCycle;
    private readonly Action _openWorkout;
    private readonly Action _openNutrition;

    [ObservableProperty] private CyclePhase _currentPhase;
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private float _targetProtein;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private bool _isBusy;

    public string PhaseLabel => CurrentPhase.ToString();

    public string TodayLabel => DateTime.Today.ToString("dddd, MMM d");

    public string Greetings
    {
        get
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12) return "Good morning";
            if (hour < 18) return "Good afternoon";
            return "Good evening";
        }
    }

    public string PhaseBadgeText => CurrentPhase switch
    {
        CyclePhase.Menstrual  => "MENSTRUAL PHASE",
        CyclePhase.Follicular => "FOLLICULAR PHASE",
        CyclePhase.Ovulatory  => "OVULATORY PHASE",
        CyclePhase.Luteal     => "LUTEAL PHASE",
        _                     => "UNKNOWN PHASE"
    };

    public string PhaseTitle => CurrentPhase switch
    {
        CyclePhase.Menstrual  => "Menstrual",
        CyclePhase.Follicular => "Follicular",
        CyclePhase.Ovulatory  => "Ovulatory",
        CyclePhase.Luteal     => "Luteal",
        _                     => "Unknown"
    };

    public string PhaseShortAdvice => CurrentPhase switch
    {
        CyclePhase.Menstrual  => "Take it easy, prioritize rest",
        CyclePhase.Follicular => "Energy rising, great time to train",
        CyclePhase.Ovulatory  => "Peak energy and strength",
        CyclePhase.Luteal     => "Moderate intensity, listen to your body",
        _                     => string.Empty
    };

    public int ReadinessScore => 75;
    public string ReadinessLabel => "Feeling energized";
    public int RecoveryScore => 82;
    public string RecoveryLabel => "Well rested";

    public float CaloriesProgress =>
        TargetCalories <= 0 ? 0f : Math.Clamp(ConsumedCalories / TargetCalories, 0f, 1f);

    public string CaloriesConsumed => $"{(int)ConsumedCalories:N0} kcal";
    public string CaloriesGoal => $"/ {(int)TargetCalories:N0} kcal";

    public string ProteinText => $"{(int)ConsumedProtein}g / {(int)TargetProtein}g";
    public float ProteinProgress => TargetProtein > 0 ? Math.Clamp(ConsumedProtein / TargetProtein, 0f, 1f) : 0f;

    public string CarbsText => $"{(int)ConsumedCarbs}g / {(int)TargetCarbs}g";
    public float CarbsProgress => TargetCarbs > 0 ? Math.Clamp(ConsumedCarbs / TargetCarbs, 0f, 1f) : 0f;

    public string FatsText => $"{(int)ConsumedFats}g / {(int)TargetFats}g";
    public float FatsProgress => TargetFats > 0 ? Math.Clamp(ConsumedFats / TargetFats, 0f, 1f) : 0f;

    public string HydrationConsumed => "1.8 L";
    public string HydrationGoal => "/ 2.5 L";
    public string SleepGoal => "8h";
    public string SessionProgressText => "UPCOMING";

    [ObservableProperty] private string _workoutTitle = "Rest Day";
    public string WorkoutSubtitle => "Upper body · Push";
    public string WorkoutDurationText => "45 min";
    public string WorkoutExercisesCount => "6";
    public string WorkoutIntensity => "Medium";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenCycleCommand { get; }
    public RelayCommand OpenWorkoutCommand { get; }
    public RelayCommand OpenNutritionCommand { get; }

    public DashboardViewModel(
        IAuthService authService,
        ICycleService cycleService,
        INutritionService nutritionService,
        IWorkoutRepository workoutRepository,
        Action openCycle,
        Action openWorkout,
        Action openNutrition)
    {
        _authService = authService;
        _cycleService = cycleService;
        _nutritionService = nutritionService;
        _workoutRepository = workoutRepository;
        _openCycle = openCycle;
        _openWorkout = openWorkout;
        _openNutrition = openNutrition;

        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        RefreshCommand = LoadDataCommand;
        OpenCycleCommand = new RelayCommand(() => _openCycle());
        OpenWorkoutCommand = new RelayCommand(() => _openWorkout());
        OpenNutritionCommand = new RelayCommand(() => _openNutrition());
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            CurrentPhase = await _cycleService.GetCurrentPhaseAsync(userId);
            NotifyPhaseProperties();

            var (calories, protein, carbs, fats) = await _nutritionService.CalculateDailyTargetsAsync(userId, CurrentPhase);
            TargetCalories = calories;
            TargetProtein = protein;
            TargetCarbs = carbs;
            TargetFats = fats;

            ConsumedCalories = await _nutritionService.GetConsumedCaloriesAsync(userId, DateTime.Today);

            var (consumedProtein, consumedCarbs, consumedFats) =
                await _nutritionService.GetConsumedMacrosAsync(userId, DateTime.Today);
            ConsumedProtein = consumedProtein;
            ConsumedCarbs = consumedCarbs;
            ConsumedFats = consumedFats;

            var plan = await _workoutRepository.GetActivePlanAsync(userId);
            WorkoutTitle = plan?.Name ?? "Rest Day";

            NotifyMacroProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyPhaseProperties()
    {
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(PhaseBadgeText));
        OnPropertyChanged(nameof(PhaseTitle));
        OnPropertyChanged(nameof(PhaseShortAdvice));
    }

    private void NotifyMacroProperties()
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesConsumed));
        OnPropertyChanged(nameof(CaloriesGoal));
        OnPropertyChanged(nameof(ProteinText));
        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(CarbsText));
        OnPropertyChanged(nameof(CarbsProgress));
        OnPropertyChanged(nameof(FatsText));
        OnPropertyChanged(nameof(FatsProgress));
    }

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        NotifyPhaseProperties();
    }

    partial void OnConsumedCaloriesChanged(float value)
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesConsumed));
    }

    partial void OnTargetCaloriesChanged(float value)
    {
        OnPropertyChanged(nameof(CaloriesProgress));
        OnPropertyChanged(nameof(CaloriesGoal));
    }
}
