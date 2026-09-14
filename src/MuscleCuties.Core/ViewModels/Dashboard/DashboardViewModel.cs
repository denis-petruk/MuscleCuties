using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject, IPageLoadAware
{
    private const string ManualPredictionSource = "manual phase log";
    private const string ProfilePhasePredictionSource = "profile phase";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ViewModelLoadGate _loadGate = new(ViewModelLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _openCycleAsync;
    private readonly Func<Task> _openDailyCheckInAsync;
    private readonly Func<Task> _openNutritionAsync;
    private readonly Func<Task> _openWorkoutAsync;
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private int _currentCycleDay;

    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Follicular;
    [ObservableProperty] private string _cycleInsightText = string.Empty;
    [ObservableProperty] private int _daysUntilPeriod;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private bool _hasActiveCycle;
    [ObservableProperty] private string _hydrationConsumed = "2.5 L";
    [ObservableProperty] private string _hydrationGoal = "target";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _needsDailyCheckIn;
    [ObservableProperty] private int _nutritionStreakDays;
    [ObservableProperty] private Color _phaseCardBackgroundColor = Color.FromArgb("#FFF1F6");
    [ObservableProperty] private Color _phaseCardDividerColor = Color.FromArgb("#EBD3DE");
    [ObservableProperty] private Color _phaseCardTextColor = Color.FromArgb("#8B4E68");
    [ObservableProperty] private int _predictedCycleLength = 28;
    [ObservableProperty] private string _readinessLabel = string.Empty;
    [ObservableProperty] private int _readinessScore;
    [ObservableProperty] private string _recoveryLabel = string.Empty;
    [ObservableProperty] private int _recoveryScore;
    [ObservableProperty] private string _sessionProgressText = "Upcoming";
    [ObservableProperty] private string _sleepGoal = "8h";
    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private float _targetProtein;
    [ObservableProperty] private bool _useDarkTheme;
    [ObservableProperty] private bool _usePhaseCardColor;

    [ObservableProperty]
    private Color _workoutActivityBackground =
        WorkoutActivityClassifier.GetBackground(WorkoutActivityClassifier.RestTag);

    [ObservableProperty]
    private Color _workoutActivityTextColor =
        WorkoutActivityClassifier.GetTextColor(WorkoutActivityClassifier.RestTag);

    [ObservableProperty] private string _workoutDurationText = "Rest day";
    [ObservableProperty] private string _workoutExercisesCount = "0";
    [ObservableProperty] private string _workoutIntensity = "Low";
    [ObservableProperty] private int _workoutStreakDays;
    [ObservableProperty] private string _workoutSubtitle = "Recovery day";
    [ObservableProperty] private string _workoutTitle = "Living happy life";
    [ObservableProperty] private ObservableCollection<WorkoutActivitySection> _workoutActivitySections = [];
    [ObservableProperty] private bool _hasMultipleActivities;
    [ObservableProperty] private bool _isRestDay = true;

    public DashboardViewModel(
        IServiceScopeFactory scopeFactory,
        Func<Task> openCycleAsync,
        Func<Task> openWorkoutAsync,
        Func<Task> openNutritionAsync,
        Func<Task> openDailyCheckInAsync)
    {
        _scopeFactory = scopeFactory;
        _openCycleAsync = openCycleAsync;
        _openWorkoutAsync = openWorkoutAsync;
        _openNutritionAsync = openNutritionAsync;
        _openDailyCheckInAsync = openDailyCheckInAsync;

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCycleCommand = new AsyncRelayCommand(_openCycleAsync);
        OpenWorkoutCommand = new AsyncRelayCommand(_openWorkoutAsync);
        OpenNutritionCommand = new AsyncRelayCommand(_openNutritionAsync);
        OpenDailyCheckInCommand = new AsyncRelayCommand(_openDailyCheckInAsync, () => NeedsDailyCheckIn);
    }

    public void Invalidate() => _loadGate.MarkStale();

    public string PhaseLabel => CurrentPhase.ToString();

    public string TodayLabel => DateTime.Today.ToString("dddd, MMM d");

    public string DashboardPhaseHeaderText => $"This week · {CurrentPhase}";

    public string PhaseStatusText => CurrentCycleDay > 0
        ? $"{CurrentPhase} · Day {CurrentCycleDay} / {PredictedCycleLength}"
        : $"{CurrentPhase} · Start tracking";

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
        CyclePhase.Menstrual => FormatPhaseBadge("Menstrual phase"),
        CyclePhase.Follicular => FormatPhaseBadge("Follicular phase"),
        CyclePhase.Ovulatory => FormatPhaseBadge("Ovulatory phase"),
        CyclePhase.Luteal => FormatPhaseBadge("Luteal phase"),
        _ => "Unknown phase"
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

    public string PhaseIconSource => CyclePhaseAssets.GetIconSource(CurrentPhase);

    public int CurrentPhaseColumn => CurrentPhase switch
    {
        CyclePhase.Menstrual => 0,
        CyclePhase.Follicular => 1,
        CyclePhase.Ovulatory => 2,
        CyclePhase.Luteal => 3,
        _ => 0
    };

    public string PhaseTimeLeftValue => CurrentCycleDay <= 0 ? "--" : $"{CalculateDaysLeftInCurrentPhase()}d";
    public string PhaseTimeLeftLabel => CurrentPhase is CyclePhase.Ovulatory ? "Peak left" : "Phase left";
    public string NextPeriodValue => DaysUntilPeriod <= 0 ? "Today" : $"{DaysUntilPeriod}d";

    public string LoadAdjustmentText => CurrentPhase switch
    {
        CyclePhase.Menstrual => "-10%",
        CyclePhase.Follicular => "+3%",
        CyclePhase.Ovulatory => "+5%",
        CyclePhase.Luteal => "-5%",
        _ => "0%"
    };

    public string WorkoutActivityTypesText =>
        WorkoutActivitySections.Count > 0
            ? string.Join(" + ", WorkoutActivitySections.Select(s => s.Title.Replace(" activity", "")))
            : "Rest";

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
    public AsyncRelayCommand OpenCycleCommand { get; }
    public AsyncRelayCommand OpenWorkoutCommand { get; }
    public AsyncRelayCommand OpenNutritionCommand { get; }
    public AsyncRelayCommand OpenDailyCheckInCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    public void RefreshThemeColors(bool useDarkTheme)
    {
        UseDarkTheme = useDarkTheme;
        RefreshPhaseCardColors();
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(ReadinessScore));
        OnPropertyChanged(nameof(RecoveryScore));
    }

    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await _loadGate.RunAsync(LoadDataCoreAsync, true);
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
            var userId = await RunScopedAsync(services =>
                services.GetRequiredService<IAuthService>().GetCurrentUserIdAsync());

            var today = DateOnly.FromDateTime(DateTime.Today);
            var profileTask = RunScopedAsync(services =>
                services.GetRequiredService<IUserRepository>().GetProfileAsync(userId));
            var readinessTask = RunScopedAsync(services =>
                services.GetRequiredService<IReadinessRepository>().GetForAsync(userId, today));
            var predictionTask = RunScopedAsync(services =>
                services.GetRequiredService<ICycleService>().GetPredictionAsync(userId));
            var consumedTask = RunScopedAsync(services =>
                services.GetRequiredService<INutritionService>()
                    .GetConsumedTotalsAsync(userId, DateTime.Today));
            var progressTask = RunScopedAsync(services =>
                services.GetRequiredService<IProgressSummaryService>()
                    .GetSummaryAsync(userId, DateTime.Today));

            var profile = await profileTask;
            DisplayName = GetFirstName(profile?.Name);

            var readinessLog = await readinessTask;
            NeedsDailyCheckIn = readinessLog is null;

            var prediction = await predictionTask ??
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

            var targetsTask = RunScopedAsync(services =>
                services.GetRequiredService<INutritionService>()
                    .CalculateDailyTargetsAsync(userId, CurrentPhase));
            var workoutTask = RunScopedAsync(services =>
                services.GetRequiredService<IWorkoutService>()
                    .GetTodaysSummaryAsync(userId, CurrentPhase, DateTime.Today));

            var (calories, protein, carbs, fats) = await targetsTask;
            TargetCalories = calories;
            TargetProtein = protein;
            TargetCarbs = carbs;
            TargetFats = fats;

            var consumed = await consumedTask;
            ConsumedCalories = consumed.Calories;
            ConsumedProtein = consumed.Protein;
            ConsumedCarbs = consumed.Carbs;
            ConsumedFats = consumed.Fats;

            var progress = await progressTask;
            WorkoutStreakDays = progress.WorkoutStreakDays;
            NutritionStreakDays = progress.NutritionStreakDays;

            var workoutSummary = await workoutTask;
            ApplyWorkoutSummary(workoutSummary);

            using var plannerScope = _scopeFactory.CreateScope();
            var dashboardPlanner = plannerScope.ServiceProvider.GetRequiredService<IDashboardPlanner>();
            ApplySupportSummary(dashboardPlanner.BuildSupportSummary(
                prediction,
                CurrentPhase,
                CaloriesProgress,
                profile?.Weight,
                profile?.WorkoutDaysPerWeek ?? 0,
                workoutSummary,
                recordedReadinessScore: readinessLog?.ReadinessScore));

            NotifyMacroProperties();
            NotifyUserLinkedProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task<T> RunScopedAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        return DataLoadScheduler.RunAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            return await operation(scope.ServiceProvider);
        });
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
        OnPropertyChanged(nameof(PhaseIconSource));
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
        IsRestDay = workoutSummary.ActivityTag == WorkoutActivityClassifier.RestTag;

        var sections = workoutSummary.ActivitySections ?? [];
        WorkoutActivitySections = new ObservableCollection<WorkoutActivitySection>(sections);
        HasMultipleActivities = sections.Count > 1;

        OnPropertyChanged(nameof(WorkoutBadgeText));
        OnPropertyChanged(nameof(IsTodaysWorkoutCompleted));
        OnPropertyChanged(nameof(WorkoutActionText));
        OnPropertyChanged(nameof(WorkoutActivityTypesText));
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

    private string FormatPhaseBadge(string phaseName)
    {
        return CurrentCycleDay > 0 ? $"Day {CurrentCycleDay} · {phaseName}" : phaseName;
    }

    private int CalculateDaysLeftInCurrentPhase()
    {
        var cycleLength = CyclePhaseRules.NormalizeCycleLength(PredictedCycleLength);
        var currentDay = Math.Clamp(CurrentCycleDay, 1, cycleLength);
        var daysLeft = 0;

        for (var offset = 1; offset <= cycleLength; offset++)
        {
            var projectedDay = (currentDay - 1 + offset) % cycleLength + 1;
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

    private static string FirstPresent(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

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

    private static bool IsManualPhasePrediction(CyclePrediction prediction)
    {
        return string.Equals(prediction.PredictionSource, ManualPredictionSource, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProfilePhasePrediction(CyclePrediction prediction)
    {
        return string.Equals(prediction.PredictionSource, ProfilePhasePredictionSource,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldUsePhaseCardColor(CyclePrediction prediction)
    {
        return prediction.HasActiveCycle || IsManualPhasePrediction(prediction) || IsProfilePhasePrediction(prediction);
    }

    private static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#5A3840" : "#F9D6D8"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#2E5230" : "#D6EED6"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#5A4A00" : "#FFF0C4"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#3E2A58" : "#E8D8F5"),
            _ => GetNeutralCardBackgroundColor(useDarkTheme)
        };
    }

    private static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#F9D6D8" : "#7A3A48"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#D6EED6" : "#3A6B3A"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#FFF0C4" : "#7A6000"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#E8D8F5" : "#5A3B80"),
            _ => GetNeutralCardTextColor(useDarkTheme)
        };
    }

    private static Color GetPhaseDividerColor(CyclePhase phase, bool useDarkTheme)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#7A4A54" : "#E8B7BE"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#447146" : "#B8D9B8"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#776516" : "#E6CF88"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#5A3F78" : "#D4BCEB"),
            _ => GetNeutralDividerColor(useDarkTheme)
        };
    }

    private static Color GetNeutralCardBackgroundColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#3A2931" : "#FFF1F6");
    }

    private static Color GetNeutralCardTextColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#F8EEF4" : "#5B4650");
    }

    private static Color GetNeutralDividerColor(bool useDarkTheme)
    {
        return Color.FromArgb(useDarkTheme ? "#5A424C" : "#EBD3DE");
    }

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

    partial void OnNeedsDailyCheckInChanged(bool value)
    {
        OpenDailyCheckInCommand.NotifyCanExecuteChanged();
    }
}
