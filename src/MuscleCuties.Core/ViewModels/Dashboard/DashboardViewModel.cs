using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private const string ManualPredictionSource = "manual phase log";
    private const string ProfilePhasePredictionSource = "profile phase";

    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly ICycleService _cycleService;
    private readonly INutritionService _nutritionService;
    private readonly IWorkoutService _workoutService;
    private readonly IProgressSummaryService _progressSummaryService;
    private readonly IDashboardPlanner _dashboardPlanner;
    private readonly IHealthSyncService _healthSyncService;
    private readonly Action _openCycle;
    private readonly Action _openWorkout;
    private readonly Action _openNutrition;
    private readonly ViewModelLoadGate _loadGate = new(TimeSpan.FromSeconds(20));

    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Follicular;
    [ObservableProperty] private bool _hasActiveCycle;
    [ObservableProperty] private bool _usePhaseCardColor;
    [ObservableProperty] private bool _useDarkTheme;
    [ObservableProperty] private Color _phaseCardBackgroundColor = Color.FromArgb("#FFF1F6");
    [ObservableProperty] private Color _phaseCardTextColor = Color.FromArgb("#8B4E68");
    [ObservableProperty] private Color _phaseCardDividerColor = Color.FromArgb("#EBD3DE");
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private float _targetProtein;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private int _currentCycleDay;
    [ObservableProperty] private int _predictedCycleLength = 28;
    [ObservableProperty] private int _daysUntilPeriod;
    [ObservableProperty] private string _cycleInsightText = string.Empty;
    [ObservableProperty] private int _readinessScore;
    [ObservableProperty] private string _readinessLabel = string.Empty;
    [ObservableProperty] private int _recoveryScore;
    [ObservableProperty] private string _recoveryLabel = string.Empty;
    [ObservableProperty] private string _sessionProgressText = "Upcoming";
    [ObservableProperty] private string _workoutTitle = "Living happy life";
    [ObservableProperty] private string _workoutSubtitle = "Recovery day";
    [ObservableProperty] private string _workoutDurationText = "Rest day";
    [ObservableProperty] private string _workoutExercisesCount = "0";
    [ObservableProperty] private string _workoutIntensity = "Low";
    [ObservableProperty] private Color _workoutActivityBackground = WorkoutActivityClassifier.GetBackground(WorkoutActivityClassifier.RestTag);
    [ObservableProperty] private Color _workoutActivityTextColor = WorkoutActivityClassifier.GetTextColor(WorkoutActivityClassifier.RestTag);
    [ObservableProperty] private string _hydrationConsumed = "2.5 L";
    [ObservableProperty] private string _hydrationGoal = "target";
    [ObservableProperty] private string _sleepGoal = "8h";
    [ObservableProperty] private int _workoutStreakDays;
    [ObservableProperty] private int _nutritionStreakDays;
    [ObservableProperty] private bool _isHealthSyncPromptVisible;
    [ObservableProperty] private bool _isHealthSyncBusy;
    [ObservableProperty] private string _healthSyncStatusText = "Not connected";
    [ObservableProperty] private string _healthSyncMessage = string.Empty;

    public string PhaseLabel => CurrentPhase.ToString();

    public string TodayLabel => DateTime.Today.ToString("dddd, MMM d");

    public string DashboardPhaseHeaderText => $"This week · {CurrentPhase}".ToUpperInvariant();

    public string PhaseStatusText => CurrentCycleDay > 0
        ? $"{CurrentPhase.ToString().ToUpperInvariant()} · DAY {CurrentCycleDay} / {PredictedCycleLength}"
        : $"{CurrentPhase.ToString().ToUpperInvariant()} · START TRACKING";

    public string Greetings
    {
        get
        {
            var hour = DateTime.Now.Hour;
            var greeting = hour switch
            {
                < 12 => "Good morning",
                < 18 => "Good afternoon",
                _ => "Good evening"
            };

            return string.IsNullOrWhiteSpace(DisplayName)
                ? greeting
                : $"{greeting}, {DisplayName}";
        }
    }

    public string PhaseBadgeText => CurrentPhase switch
    {
        CyclePhase.Menstrual => FormatPhaseBadge("MENSTRUAL PHASE"),
        CyclePhase.Follicular => FormatPhaseBadge("FOLLICULAR PHASE"),
        CyclePhase.Ovulatory => FormatPhaseBadge("OVULATORY PHASE"),
        CyclePhase.Luteal => FormatPhaseBadge("LUTEAL PHASE"),
        _ => "UNKNOWN PHASE"
    };

    public string PhaseTitle => CurrentPhase switch
    {
        CyclePhase.Menstrual => "Menstrual",
        CyclePhase.Follicular => "Follicular",
        CyclePhase.Ovulatory => "Ovulatory",
        CyclePhase.Luteal => "Luteal",
        _ => "Unknown"
    };

    public string PhaseCardTitle => CurrentPhase switch
    {
        CyclePhase.Menstrual => "Recovery rhythm",
        CyclePhase.Follicular => "Build momentum",
        CyclePhase.Ovulatory => "Your power window",
        CyclePhase.Luteal => "Steady strength window",
        _ => "Cycle window"
    };

    public string PhaseShortAdvice => CurrentPhase switch
    {
        CyclePhase.Menstrual => FirstPresent(CycleInsightText, "Take it easy, prioritize rest"),
        CyclePhase.Follicular => FirstPresent(CycleInsightText, "Energy rising, great time to train"),
        CyclePhase.Ovulatory => FirstPresent(CycleInsightText, "Peak energy and strength"),
        CyclePhase.Luteal => FirstPresent(CycleInsightText, "Moderate intensity, listen to your body"),
        _ => string.Empty
    };

    public string PhaseIllustrationSource => CyclePhaseAssets.GetVisualSource(CurrentPhase);
    public bool PhaseIllustrationUsesAnimation => CyclePhaseAssets.UsesAnimatedVisual(CurrentPhase);

    public int CurrentPhaseColumn => CurrentPhase switch
    {
        CyclePhase.Menstrual => 0,
        CyclePhase.Follicular => 1,
        CyclePhase.Ovulatory => 2,
        CyclePhase.Luteal => 3,
        _ => 0
    };

    public string PhaseTimeLeftValue => CurrentCycleDay <= 0 ? "--" : $"{CalculateDaysLeftInCurrentPhase()}d";
    public string PhaseTimeLeftLabel => CurrentPhase is CyclePhase.Ovulatory ? "PEAK LEFT" : "PHASE LEFT";
    public string NextPeriodValue => DaysUntilPeriod <= 0 ? "Today" : $"{DaysUntilPeriod}d";
    public string LoadAdjustmentText => CurrentPhase switch
    {
        CyclePhase.Menstrual => "-10%",
        CyclePhase.Follicular => "+3%",
        CyclePhase.Ovulatory => "+5%",
        CyclePhase.Luteal => "-5%",
        _ => "0%"
    };

    public string WorkoutBadgeText => IsTodaysWorkoutCompleted
        ? "Workout completed"
        : $"Today · {SessionProgressText}";
    public bool IsTodaysWorkoutCompleted =>
        string.Equals(SessionProgressText, "Completed", StringComparison.OrdinalIgnoreCase);
    public string WorkoutActionText => IsTodaysWorkoutCompleted
        ? "Edit workout"
        : string.Equals(SessionProgressText, "REST", StringComparison.OrdinalIgnoreCase)
            ? "Log rest day"
            : "Start workout";
    public string WorkoutStreakText => WorkoutStreakDays == 1
        ? "1 day session streak"
        : $"{WorkoutStreakDays} day session streak";
    public string NutritionStreakText => NutritionStreakDays == 1
        ? "1 day log streak"
        : $"{NutritionStreakDays} day log streak";

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

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenCycleCommand { get; }
    public RelayCommand OpenWorkoutCommand { get; }
    public RelayCommand OpenNutritionCommand { get; }
    public AsyncRelayCommand ConnectAppleHealthCommand { get; }
    public AsyncRelayCommand ConnectWhoopCommand { get; }
    public AsyncRelayCommand DismissHealthSyncPromptCommand { get; }

    public void RefreshThemeColors(bool useDarkTheme)
    {
        UseDarkTheme = useDarkTheme;
        RefreshPhaseCardColors();
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(ReadinessScore));
        OnPropertyChanged(nameof(RecoveryScore));
    }

    public DashboardViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        ICycleService cycleService,
        INutritionService nutritionService,
        IWorkoutService workoutService,
        IProgressSummaryService progressSummaryService,
        IDashboardPlanner dashboardPlanner,
        IHealthSyncService healthSyncService,
        Action openCycle,
        Action openWorkout,
        Action openNutrition)
    {
        _authService = authService;
        _userRepository = userRepository;
        _cycleService = cycleService;
        _nutritionService = nutritionService;
        _workoutService = workoutService;
        _progressSummaryService = progressSummaryService;
        _dashboardPlanner = dashboardPlanner;
        _healthSyncService = healthSyncService;
        _openCycle = openCycle;
        _openWorkout = openWorkout;
        _openNutrition = openNutrition;

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCycleCommand = new RelayCommand(() => _openCycle());
        OpenWorkoutCommand = new RelayCommand(() => _openWorkout());
        OpenNutritionCommand = new RelayCommand(() => _openNutrition());
        ConnectAppleHealthCommand = new AsyncRelayCommand(() => ConnectHealthAsync(HealthDataSource.AppleHealth));
        ConnectWhoopCommand = new AsyncRelayCommand(() => ConnectHealthAsync(HealthDataSource.Whoop));
        DismissHealthSyncPromptCommand = new AsyncRelayCommand(DismissHealthSyncPromptAsync);
    }

    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await _loadGate.RunAsync(LoadDataCoreAsync, force: true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var profile = await _userRepository.GetProfileAsync(userId);
            DisplayName = GetFirstName(profile?.Name);
            var healthSummary = await _healthSyncService.GetCachedWeeklySummaryAsync(userId);
            var healthStatus = await _healthSyncService.GetStatusAsync(userId);
            HealthSyncStatusText = healthStatus.SummaryText;
            IsHealthSyncPromptVisible = await _healthSyncService.ShouldShowPromptAsync(userId);

            var prediction = await _cycleService.GetPredictionAsync(userId) ??
                             new CyclePrediction
                             {
                                 CurrentPhase = CyclePhase.Follicular,
                                 PredictedCycleLength = profile?.CycleLength > 0 ? profile.CycleLength : 28,
                                 PredictionSource = "default"
            };
            HasActiveCycle = prediction.HasActiveCycle;
            UsePhaseCardColor = ShouldUsePhaseCardColor(prediction);
            CurrentPhase = prediction.CurrentPhase;
            CurrentCycleDay = prediction.CurrentDay;
            PredictedCycleLength = prediction.PredictedCycleLength;
            DaysUntilPeriod = prediction.DaysUntilPeriod;
            RefreshPhaseCardColors();
            NotifyPhaseProperties();

            var (calories, protein, carbs, fats) = await _nutritionService.CalculateDailyTargetsAsync(userId, CurrentPhase);
            TargetCalories = calories;
            TargetProtein = protein;
            TargetCarbs = carbs;
            TargetFats = fats;

            var consumed = await _nutritionService.GetConsumedTotalsAsync(userId, DateTime.Today);
            ConsumedCalories = consumed.Calories;
            ConsumedProtein = consumed.Protein;
            ConsumedCarbs = consumed.Carbs;
            ConsumedFats = consumed.Fats;

            var progress = await _progressSummaryService.GetSummaryAsync(userId, DateTime.Today);
            WorkoutStreakDays = progress.WorkoutStreakDays;
            NutritionStreakDays = progress.NutritionStreakDays;

            var workoutSummary = await _workoutService.GetTodaysSummaryAsync(userId, CurrentPhase, DateTime.Today);
            ApplyWorkoutSummary(workoutSummary);

            ApplySupportSummary(_dashboardPlanner.BuildSupportSummary(
                prediction,
                CurrentPhase,
                CaloriesProgress,
                profile?.Weight,
                profile?.WorkoutDaysPerWeek ?? 0,
                workoutSummary,
                healthSummary));

            NotifyMacroProperties();
            NotifyUserLinkedProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectHealthAsync(HealthDataSource source)
    {
        IsHealthSyncBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var result = await _healthSyncService.SyncAsync(userId, source);
            HealthSyncMessage = result.Message;
            IsHealthSyncPromptVisible = !result.IsConnected;

            if (result.IsConnected)
                await _loadGate.RunAsync(LoadDataCoreAsync, force: true);
        }
        finally
        {
            IsHealthSyncBusy = false;
        }
    }

    private async Task DismissHealthSyncPromptAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        await _healthSyncService.DismissPromptAsync(userId);
        IsHealthSyncPromptVisible = false;
    }

    private void NotifyPhaseProperties()
    {
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(DashboardPhaseHeaderText));
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseBadgeText));
        OnPropertyChanged(nameof(PhaseTitle));
        OnPropertyChanged(nameof(PhaseCardTitle));
        OnPropertyChanged(nameof(PhaseShortAdvice));
        OnPropertyChanged(nameof(PhaseIllustrationSource));
        OnPropertyChanged(nameof(PhaseIllustrationUsesAnimation));
        OnPropertyChanged(nameof(CurrentPhaseColumn));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
        OnPropertyChanged(nameof(PhaseTimeLeftLabel));
        OnPropertyChanged(nameof(LoadAdjustmentText));
        OnPropertyChanged(nameof(PhaseCardBackgroundColor));
        OnPropertyChanged(nameof(PhaseCardTextColor));
        OnPropertyChanged(nameof(PhaseCardDividerColor));
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

    private void NotifyUserLinkedProperties()
    {
        OnPropertyChanged(nameof(Greetings));
        OnPropertyChanged(nameof(PhaseBadgeText));
        OnPropertyChanged(nameof(PhaseShortAdvice));
    }

    private void ApplyWorkoutSummary(TodaysWorkoutSummary workoutSummary)
    {
        WorkoutTitle = workoutSummary.Title;
        WorkoutSubtitle = workoutSummary.Subtitle;
        WorkoutDurationText = workoutSummary.DurationText;
        WorkoutExercisesCount = workoutSummary.ExercisesCount;
        WorkoutIntensity = workoutSummary.Intensity;
        SessionProgressText = workoutSummary.SessionProgressText;
        WorkoutActivityBackground = WorkoutActivityClassifier.GetBackground(workoutSummary.ActivityTag);
        WorkoutActivityTextColor = WorkoutActivityClassifier.GetTextColor(workoutSummary.ActivityTag);
        OnPropertyChanged(nameof(WorkoutBadgeText));
        OnPropertyChanged(nameof(IsTodaysWorkoutCompleted));
        OnPropertyChanged(nameof(WorkoutActionText));
    }

    private void ApplySupportSummary(DashboardSupportSummary supportSummary)
    {
        CycleInsightText = supportSummary.CycleInsightText;
        HydrationConsumed = supportSummary.HydrationConsumed;
        HydrationGoal = supportSummary.HydrationGoal;
        SleepGoal = supportSummary.SleepGoal;
        ReadinessScore = supportSummary.ReadinessScore;
        ReadinessLabel = supportSummary.ReadinessLabel;
        RecoveryScore = supportSummary.RecoveryScore;
        RecoveryLabel = supportSummary.RecoveryLabel;
    }

    private string FormatPhaseBadge(string phaseName) =>
        CurrentCycleDay > 0 ? $"DAY {CurrentCycleDay} · {phaseName}" : phaseName;

    private int CalculateDaysLeftInCurrentPhase()
    {
        var cycleLength = CyclePhaseRules.NormalizeCycleLength(PredictedCycleLength);
        var currentDay = Math.Clamp(CurrentCycleDay, 1, cycleLength);
        var daysLeft = 0;

        for (var offset = 1; offset <= cycleLength; offset++)
        {
            var projectedDay = ((currentDay - 1 + offset) % cycleLength) + 1;
            if (CyclePhaseRules.CalculatePhase(projectedDay, cycleLength) != CurrentPhase)
                break;

            daysLeft++;
        }

        return daysLeft;
    }

    private static string GetFirstName(string? fullName)
    {
        var trimmed = fullName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        return trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private void RefreshPhaseCardColors()
    {
        PhaseCardBackgroundColor = UsePhaseCardColor
            ? GetPhaseBackgroundColor(CurrentPhase, UseDarkTheme)
            : GetNeutralCardBackgroundColor(UseDarkTheme);
        PhaseCardTextColor = UsePhaseCardColor
            ? GetPhaseTextColor(CurrentPhase, UseDarkTheme)
            : GetNeutralCardTextColor(UseDarkTheme);
        PhaseCardDividerColor = UsePhaseCardColor
            ? GetPhaseDividerColor(CurrentPhase, UseDarkTheme)
            : GetNeutralDividerColor(UseDarkTheme);
    }

    private static bool IsManualPhasePrediction(CyclePrediction prediction) =>
        string.Equals(prediction.PredictionSource, ManualPredictionSource, StringComparison.OrdinalIgnoreCase);

    private static bool IsProfilePhasePrediction(CyclePrediction prediction) =>
        string.Equals(prediction.PredictionSource, ProfilePhasePredictionSource, StringComparison.OrdinalIgnoreCase);

    private static bool ShouldUsePhaseCardColor(CyclePrediction prediction) =>
        prediction.HasActiveCycle || IsManualPhasePrediction(prediction) || IsProfilePhasePrediction(prediction);

    private static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme) => phase switch
    {
        CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#5A3840" : "#F9D6D8"),
        CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#2E5230" : "#D6EED6"),
        CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#5A4A00" : "#FFF0C4"),
        CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#3E2A58" : "#E8D8F5"),
        _ => GetNeutralCardBackgroundColor(useDarkTheme)
    };

    private static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme) => phase switch
    {
        CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#F9D6D8" : "#7A3A48"),
        CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#D6EED6" : "#3A6B3A"),
        CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#FFF0C4" : "#7A6000"),
        CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#E8D8F5" : "#5A3B80"),
        _ => GetNeutralCardTextColor(useDarkTheme)
    };

    private static Color GetPhaseDividerColor(CyclePhase phase, bool useDarkTheme) => phase switch
    {
        CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#7A4A54" : "#E8B7BE"),
        CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#447146" : "#B8D9B8"),
        CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#776516" : "#E6CF88"),
        CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#5A3F78" : "#D4BCEB"),
        _ => GetNeutralDividerColor(useDarkTheme)
    };

    private static Color GetNeutralCardBackgroundColor(bool useDarkTheme) =>
        Color.FromArgb(useDarkTheme ? "#3A2931" : "#FFF1F6");

    private static Color GetNeutralCardTextColor(bool useDarkTheme) =>
        Color.FromArgb(useDarkTheme ? "#F8EEF4" : "#5B4650");

    private static Color GetNeutralDividerColor(bool useDarkTheme) =>
        Color.FromArgb(useDarkTheme ? "#5A424C" : "#EBD3DE");

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        RefreshPhaseCardColors();
        NotifyPhaseProperties();
    }

    partial void OnUsePhaseCardColorChanged(bool value)
    {
        RefreshPhaseCardColors();
    }

    partial void OnUseDarkThemeChanged(bool value)
    {
        RefreshPhaseCardColors();
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

    partial void OnDisplayNameChanged(string value)
    {
        OnPropertyChanged(nameof(Greetings));
    }

    partial void OnCurrentCycleDayChanged(int value)
    {
        OnPropertyChanged(nameof(PhaseBadgeText));
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
    }

    partial void OnPredictedCycleLengthChanged(int value)
    {
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
    }

    partial void OnDaysUntilPeriodChanged(int value)
    {
        OnPropertyChanged(nameof(NextPeriodValue));
    }

    partial void OnCycleInsightTextChanged(string value)
    {
        OnPropertyChanged(nameof(PhaseShortAdvice));
    }

    partial void OnSessionProgressTextChanged(string value)
    {
        OnPropertyChanged(nameof(WorkoutBadgeText));
        OnPropertyChanged(nameof(IsTodaysWorkoutCompleted));
        OnPropertyChanged(nameof(WorkoutActionText));
    }

    partial void OnWorkoutStreakDaysChanged(int value)
    {
        OnPropertyChanged(nameof(WorkoutStreakText));
    }

    partial void OnNutritionStreakDaysChanged(int value)
    {
        OnPropertyChanged(nameof(NutritionStreakText));
    }
}
