using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly INutritionService _nutritionService;
    private readonly IWorkoutService _workoutService;
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

    [ObservableProperty] private string _workoutTitle = "Rest Day";
    [ObservableProperty] private string _workoutSubtitle = string.Empty;
    [ObservableProperty] private string _workoutDurationText = string.Empty;
    [ObservableProperty] private string _workoutIntensity = string.Empty;
    [ObservableProperty] private string _workoutExercisesCount = string.Empty;

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

    // TODO: replace heuristic with HRV / resting HR from Apple Watch, Garmin, or Fitbit once wearable integration is added
    [ObservableProperty] private int _readinessScore;
    [ObservableProperty] private string _readinessLabel = string.Empty;
    [ObservableProperty] private int _recoveryScore;
    [ObservableProperty] private string _recoveryLabel = string.Empty;

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

    // TODO(frontend-blocked): HydrationConsumed is hardcoded, bound to DashboardPage.xaml:474
    public string HydrationConsumed => "1.8 L";
    public string HydrationGoal => "/ 2.5 L";
    // TODO(frontend-blocked): SleepGoal is hardcoded
    public string SleepGoal => "8h";

    [ObservableProperty] private string _sessionProgressText = string.Empty;
    [ObservableProperty] private bool _isRestDay;

    public bool IsActiveWorkoutDay => !IsRestDay;

    partial void OnIsRestDayChanged(bool value)
    {
        OnPropertyChanged(nameof(IsActiveWorkoutDay));
    }

    private static string BuildWorkoutSubtitle(WorkoutDay day) => day.WorkoutType switch
    {
        WorkoutType.Cardio   => $"{day.WorkoutDayExercises.Count} rounds · {day.DurationMinutes} min",
        WorkoutType.Recovery => $"{day.WorkoutDayExercises.Count} stretches · {day.DurationMinutes} min",
        _                    => $"{day.WorkoutDayExercises.Count} exercises · {day.DurationMinutes} min"
    };

    private static string GetDashboardRecoveryCopy(CyclePhase phase, RecoveryType recoveryType) =>
        (phase, recoveryType) switch
        {
            (CyclePhase.Menstrual, _)                         => "Your body is resetting. Full rest today.",
            (CyclePhase.Luteal, RecoveryType.PassiveRecovery) => "Wind down gently. Full rest supports recovery.",
            (_, RecoveryType.ActiveRecovery)                  => "Light movement to keep energy flowing.",
            _                                                 => "Recovery day."
        };

    private static string GetIntensityLabel(WorkoutType type, CyclePhase phase) => type switch
    {
        WorkoutType.Recovery => "GENTLE",
        WorkoutType.Cardio   => phase switch
        {
            CyclePhase.Ovulatory  => "HIGH",
            CyclePhase.Follicular => "MODERATE",
            _                     => "LOW"
        },
        _ => phase switch
        {
            CyclePhase.Ovulatory  => "PEAK",
            CyclePhase.Follicular => "HIGH",
            CyclePhase.Luteal     => "MODERATE",
            CyclePhase.Menstrual  => "LOW",
            _                     => "MODERATE"
        }
    };

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenCycleCommand { get; }
    public RelayCommand OpenWorkoutCommand { get; }
    public RelayCommand OpenNutritionCommand { get; }

    public DashboardViewModel(
        IAuthService authService,
        ICycleService cycleService,
        INutritionService nutritionService,
        IWorkoutService workoutService,
        Action openCycle,
        Action openWorkout,
        Action openNutrition)
    {
        _authService = authService;
        _cycleService = cycleService;
        _nutritionService = nutritionService;
        _workoutService = workoutService;
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

            var todaysDay = await _workoutService.GetTodaysWorkoutAsync(userId);
            if (todaysDay is not null)
            {
                var isRecovery = todaysDay.WorkoutType == WorkoutType.Recovery;
                IsRestDay = isRecovery;

                WorkoutTitle = isRecovery
                    ? (todaysDay.RecoveryType == RecoveryType.PassiveRecovery ? "Rest & Restore" : "Active Recovery")
                    : todaysDay.Name;

                WorkoutSubtitle = isRecovery
                    ? GetDashboardRecoveryCopy(CurrentPhase, todaysDay.RecoveryType)
                    : BuildWorkoutSubtitle(todaysDay);

                WorkoutDurationText   = isRecovery ? string.Empty : $"{todaysDay.DurationMinutes} min";
                WorkoutIntensity      = isRecovery ? string.Empty : GetIntensityLabel(todaysDay.WorkoutType, CurrentPhase);
                WorkoutExercisesCount = isRecovery ? string.Empty : todaysDay.WorkoutDayExercises.Count.ToString();

                SessionProgressText = isRecovery
                    ? (todaysDay.RecoveryType == RecoveryType.PassiveRecovery ? "PASSIVE RECOVERY" : "ACTIVE RECOVERY")
                    : todaysDay.WorkoutType.ToString().ToUpper();
            }
            else
            {
                IsRestDay             = true;
                WorkoutTitle          = "Rest Day";
                WorkoutSubtitle       = "No workout scheduled today";
                WorkoutDurationText   = string.Empty;
                WorkoutIntensity      = string.Empty;
                WorkoutExercisesCount = string.Empty;
                SessionProgressText   = "REST DAY";
            }

            // TODO: factor in actual sleep hours once UserProfile stores them (or wearable provides them)
            ReadinessScore = ComputeReadinessScore(CurrentPhase, IsRestDay);
            ReadinessLabel = ReadinessLabelFor(ReadinessScore);
            RecoveryScore  = ComputeRecoveryScore(CurrentPhase, IsRestDay);
            RecoveryLabel  = RecoveryLabelFor(RecoveryScore);

            NotifyMacroProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Phase gives the base score; rest days get a small boost since there is no workout stress today.
    // TODO: replace with Apple Watch / Garmin HRV data when wearable integration is added
    private static int ComputeReadinessScore(CyclePhase phase, bool isRestDay)
    {
        var baseScore = phase switch
        {
            CyclePhase.Ovulatory  => 85,
            CyclePhase.Follicular => 78,
            CyclePhase.Luteal     => 65,
            CyclePhase.Menstrual  => 55,
            _                     => 70
        };
        return Math.Clamp(baseScore + (isRestDay ? 5 : 0), 0, 100);
    }

    // TODO: replace with Apple Watch / Garmin recovery metrics when wearable integration is added
    private static int ComputeRecoveryScore(CyclePhase phase, bool isRestDay)
    {
        var baseScore = phase switch
        {
            CyclePhase.Ovulatory  => 88,
            CyclePhase.Follicular => 80,
            CyclePhase.Luteal     => 70,
            CyclePhase.Menstrual  => 60,
            _                     => 72
        };
        return Math.Clamp(baseScore + (isRestDay ? 8 : -5), 0, 100);
    }

    private static string ReadinessLabelFor(int score) => score switch
    {
        >= 80 => "Feeling energized",
        >= 65 => "Ready to move",
        >= 50 => "Take it steady",
        _     => "Rest & restore"
    };

    private static string RecoveryLabelFor(int score) => score switch
    {
        >= 80 => "Well rested",
        >= 65 => "Recovering well",
        >= 50 => "Moderate fatigue",
        _     => "Needs more rest"
    };

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
