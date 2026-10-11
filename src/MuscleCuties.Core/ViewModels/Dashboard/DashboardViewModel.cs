using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Common;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Models.UI.Dashboard;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Common;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.Core.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject, IPageLoadAware
{
    private const string ManualPredictionSource = "manual phase log";
    private const string ProfilePhasePredictionSource = "profile phase";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ViewModelLoadGate _loadGate = new(ViewModelLoadGate.PageFreshnessWindow);
    // Suppresses per-field partial On*Changed notifications during a bulk load so that
    // computed properties are only invalidated once at the end via the Notify* helpers.
    private bool _isBulkLoading;
    private readonly Func<Task> _openCycleAsync;
    private readonly Func<Task> _openDailyCheckInAsync;
    private readonly Func<Task> _openNutritionAsync;
    private readonly Func<Task> _openWorkoutAsync;
    private readonly Func<int, Task> _openWorkoutSessionAsync;
    private readonly Func<Task>? _openPhotoVaultAsync;
    private int _todaysWorkoutDayId;
    private InjuryLogViewModel? _injuryLogVm;
    [ObservableProperty] private bool _isInjuryModalVisible;
    [ObservableProperty] private bool _hasActiveInjuries;
    [ObservableProperty] private bool _hasTodayPhoto;
    [ObservableProperty] private string _photoCheckInText = "Add today's progress photo";
    [ObservableProperty] private string _photoButtonText = "Check in";
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private int _currentCycleDay;
    [ObservableProperty] private int _cycleCount;

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
    [ObservableProperty] private IReadOnlyList<DashboardTrendPoint> _trendPoints = [];
    [ObservableProperty] private bool _hasWeightTrend;
    [ObservableProperty] private bool _hasReadinessTrend;
    [ObservableProperty] private string _weightTrendSummary = "Add weight at three check-ins to see your trend";
    [ObservableProperty] private string _readinessTrendSummary = "Complete a check-in to see readiness history";
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
        Func<int, Task> openWorkoutSessionAsync,
        Func<Task> openNutritionAsync,
        Func<Task> openDailyCheckInAsync,
        INutritionChangeNotifier? changeNotifier = null,
        Func<Task>? openPhotoVaultAsync = null)
    {
        _scopeFactory = scopeFactory;
        _openCycleAsync = openCycleAsync;
        _openWorkoutAsync = openWorkoutAsync;
        _openWorkoutSessionAsync = openWorkoutSessionAsync;
        _openNutritionAsync = openNutritionAsync;
        _openDailyCheckInAsync = openDailyCheckInAsync;
        _openPhotoVaultAsync = openPhotoVaultAsync;

        if (changeNotifier is not null)
        {
            changeNotifier.NutritionChanged += OnNutritionChanged;
        }

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCycleCommand = new AsyncRelayCommand(_openCycleAsync);
        OpenWorkoutCommand = new AsyncRelayCommand(_openWorkoutAsync);
        StartWorkoutCommand = new AsyncRelayCommand(StartWorkoutAsync);
        OpenNutritionCommand = new AsyncRelayCommand(_openNutritionAsync);
        OpenDailyCheckInCommand = new AsyncRelayCommand(_openDailyCheckInAsync, () => NeedsDailyCheckIn);
        OpenPhotoVaultCommand = new AsyncRelayCommand(OpenPhotoVaultAsync);
    }

    public AsyncRelayCommand OpenPhotoVaultCommand { get; }

    private Task OpenPhotoVaultAsync() => _openPhotoVaultAsync?.Invoke() ?? Task.CompletedTask;

    private void OnNutritionChanged(object? sender, NutritionChangedEventArgs e)
    {
        _loadGate.InvalidateAndReloadIfToday(e.Date, LoadDataCoreAsync, this);
    }

    public void Invalidate() => _loadGate.MarkStale();

    public Task OnProfileChangedAsync()
    {
        Invalidate();
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    public void OnProfileChanged() => _ = OnProfileChangedAsync();

    public Task OnCycleChangedAsync()
    {
        Invalidate();
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    public void OnCycleChanged() => _ = OnCycleChangedAsync();

    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _scopeFactory, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    [RelayCommand]
    private Task OpenInjuryModalAsync() => InjuryModalCoordinator.OpenModalAsync(InjuryLogVm, v => IsInjuryModalVisible = v);

    [RelayCommand]
    private void CloseInjuryModal() => InjuryModalCoordinator.CloseModal(_injuryLogVm, v => IsInjuryModalVisible = v);

    private async Task RefreshAfterInjuryChangeAsync()
    {
        HasActiveInjuries = InjuryLogVm.HasActiveInjuries;
        Invalidate();
        await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    public string DashboardPhaseHeaderText => $"This week · {CurrentPhase}";

    public string HormonalSummary => !HasActiveCycle
        ? "Log your cycle to see typical hormone patterns for each phase."
        : CurrentPhase switch
        {
            CyclePhase.Menstrual => "Estrogen and progesterone are typically low. Your own symptoms matter most.",
            CyclePhase.Follicular => "Estrogen typically rises during this phase; energy can vary day to day.",
            CyclePhase.Ovulatory => "Estrogen typically peaks around ovulation; timing varies by person.",
            CyclePhase.Luteal => "Progesterone typically rises, then falls before a period. Rest needs can vary.",
            _ => "Hormone patterns vary; a phase estimate cannot measure your hormone levels."
        };

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

    public string WorkoutBadgeText => IsTodaysWorkoutCompleted
        ? "Workout completed"
        : $"Today · {SessionProgressText}";

    public bool IsTodaysWorkoutCompleted =>
        string.Equals(SessionProgressText, "Completed", StringComparison.OrdinalIgnoreCase);

    public string WorkoutStreakText => WorkoutStreakDays == 1
        ? "1 day session streak"
        : $"{WorkoutStreakDays} day session streak";

    public string NutritionStreakText => NutritionStreakDays == 1
        ? "1 day log streak"
        : $"{NutritionStreakDays} day log streak";

    public int CompositeScore => (ReadinessScore + RecoveryScore) / 2;
    public bool HasApprovedScorePolicy => false;
    public string ScoreSourceLabel => "Numerical readiness and recovery guidance is unavailable pending qualified review. You can still record a daily check-in.";
    public bool HasAnyTrend => HasWeightTrend || HasApprovedScorePolicy && HasReadinessTrend;

    public float CaloriesProgress =>
        MacroProgressFormatter.CalculateProgress(ConsumedCalories, TargetCalories);

    public string CaloriesConsumed => MacroProgressFormatter.FormatCaloriesConsumed(ConsumedCalories);
    public string CaloriesGoal => MacroProgressFormatter.FormatCaloriesGoal(TargetCalories);

    public string ProteinText => MacroProgressFormatter.FormatGramsText(ConsumedProtein, TargetProtein);
    public float ProteinProgress => MacroProgressFormatter.CalculateProgress(ConsumedProtein, TargetProtein);

    public string CarbsText => MacroProgressFormatter.FormatGramsText(ConsumedCarbs, TargetCarbs);
    public float CarbsProgress => MacroProgressFormatter.CalculateProgress(ConsumedCarbs, TargetCarbs);

    public string FatsText => MacroProgressFormatter.FormatGramsText(ConsumedFats, TargetFats);
    public float FatsProgress => MacroProgressFormatter.CalculateProgress(ConsumedFats, TargetFats);

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand OpenCycleCommand { get; }
    public AsyncRelayCommand OpenWorkoutCommand { get; }
    public AsyncRelayCommand StartWorkoutCommand { get; }
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
            await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private Task StartWorkoutAsync() =>
        IsRestDay || _todaysWorkoutDayId <= 0
            ? _openWorkoutAsync()
            : _openWorkoutSessionAsync(_todaysWorkoutDayId);

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await RunScopedAsync(services =>
                services.GetRequiredService<IAuthService>().GetCurrentUserIdAsync());

            var today = DateOnly.FromDateTime(DateTime.Today);
            var profileTask = RunScopedAsync(services =>
                services.GetRequiredService<IProfileSettingsService>().GetProfileAsync(userId));
            var readinessTask = RunScopedAsync(services =>
                services.GetRequiredService<IDailyCheckInDataService>().GetCheckInAsync(userId, today));
            var injuriesTask = RunScopedAsync(services =>
                services.GetRequiredService<IWorkoutInjuryService>().GetActiveAsync(userId));
            var predictionTask = RunScopedAsync(services =>
                services.GetRequiredService<ICycleService>().GetPredictionAsync(userId));
            var consumedTask = RunScopedAsync(services =>
                services.GetRequiredService<INutritionService>()
                    .GetConsumedTotalsAsync(userId, DateTime.Today));
            var progressTask = RunScopedAsync(services =>
                services.GetRequiredService<IProgressSummaryService>()
                    .GetSummaryAsync(userId, DateTime.Today));
            var cycleHistoryTask = RunScopedAsync(services =>
                services.GetRequiredService<ICycleService>().GetCycleHistoryAsync(userId));
            var trendHistoryTask = RunScopedAsync(services =>
                services.GetRequiredService<IDailyCheckInDataService>()
                    .GetCheckInsAsync(userId, today.AddDays(-12), today));
            var photosTask = RunScopedAsync(services =>
                services.GetService<IPhotoVaultService>()?.GetPhotosAsync(userId) ??
                Task.FromResult<IReadOnlyList<PhotoJourneyEntry>>([]));

            // Observe every worker before leaving this load, even if one fails.
            await Task.WhenAll(profileTask, readinessTask, injuriesTask, predictionTask, consumedTask,
                progressTask, cycleHistoryTask, trendHistoryTask, photosTask);

            var profile = await profileTask;
            var readinessLog = await readinessTask;
            var prediction = await predictionTask ??
                             new CyclePrediction
                             {
                                 CurrentPhase = CyclePhase.Follicular,
                                 PredictedCycleLength = profile?.CycleLength > 0 ? profile.CycleLength : 28,
                                 PredictionSource = "default"
                             };

            _isBulkLoading = true;
            DisplayName = GetFirstName(profile?.Name);
            NeedsDailyCheckIn = readinessLog is null;
            HasActiveInjuries = (await injuriesTask).Count > 0;
            HasActiveCycle = prediction.HasActiveCycle;
            UsePhaseCardColor = ShouldUsePhaseCardColor(prediction);
            CurrentPhase = prediction.CurrentPhase;
            CurrentCycleDay = prediction.CurrentDay;
            PredictedCycleLength = prediction.PredictedCycleLength;
            DaysUntilPeriod = prediction.DaysUntilPeriod;
            _isBulkLoading = false;
            RefreshPhaseCardColors();
            NotifyPhaseProperties();
            NotifyUserLinkedProperties();

            var targetsTask = RunScopedAsync(services =>
                services.GetRequiredService<INutritionService>()
                    .CalculateDailyTargetsAsync(userId, CurrentPhase));
            var workoutTask = RunScopedAsync(services =>
                services.GetRequiredService<IWorkoutService>()
                    .GetTodaysSummaryAsync(userId, CurrentPhase, DateTime.Today));

            await Task.WhenAll(targetsTask, workoutTask);

            _isBulkLoading = true;
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
            CycleCount = (await cycleHistoryTask).Count;
            var photos = await photosTask;
            HasTodayPhoto = photos.Any(p => p.Date.Date == DateTime.Today.Date);
            PhotoCheckInText = HasTodayPhoto
                ? "Progress photo logged for today"
                : "Add today's progress photo";
            PhotoButtonText = HasTodayPhoto ? "View vault" : "Check in";
            _isBulkLoading = false;

            var trendLogs = await trendHistoryTask;
            var trend = DashboardTrendPlanner.Build(trendLogs, today);
            TrendPoints = trend;
            HasWeightTrend = trend.Any(point => point.WeightKg.HasValue);
            HasReadinessTrend = trend.Any(point => point.ReadinessScore.HasValue);
            OnPropertyChanged(nameof(HasAnyTrend));
            var lastWeight = trend.LastOrDefault(point => point.WeightKg.HasValue)?.WeightKg;
            WeightTrendSummary = lastWeight is null
                ? "Add weight at three check-ins to see your trend"
                : $"7-day median {lastWeight.Value:N1} kg";
            ReadinessTrendSummary = HasReadinessTrend
                ? $"{trend.Count(point => point.ReadinessScore.HasValue)} of 7 days checked in"
                : "Complete a check-in to see readiness history";

            var workoutSummary = await workoutTask;
            ApplyWorkoutSummary(workoutSummary);

            var recordedSleepLogs = trendLogs
                .Where(log => log.SleepHours > 0 && double.IsFinite(log.SleepHours))
                .ToList();
            double? localAverageSleepHours = recordedSleepLogs.Count > 0
                ? recordedSleepLogs.Average(log => log.SleepHours)
                : null;
            int? localSteps = readinessLog?.StepsYesterday > 0
                ? readinessLog.StepsYesterday
                : (readinessLog?.Steps7dAvg > 0
                    ? readinessLog.Steps7dAvg
                    : trendLogs.Where(log => log.StepsYesterday > 0).Select(log => (int?)log.StepsYesterday).LastOrDefault());

            using var plannerScope = _scopeFactory.CreateScope();
            var orchestrator = plannerScope.ServiceProvider.GetService<IReadinessSyncOrchestrator>();
            if (orchestrator is not null)
            {
                var supportSummary = await orchestrator.ComputeDashboardSupportAsync(
                    userId,
                    prediction,
                    CurrentPhase,
                    CaloriesProgress,
                    profile?.Weight,
                    profile?.WorkoutDaysPerWeek ?? -1,
                    workoutSummary,
                    readinessLog?.ReadinessScore,
                    HasActiveInjuries,
                    localSteps,
                    localAverageSleepHours);
                ApplySupportSummary(supportSummary);
            }
            else
            {
                var dashboardPlanner = plannerScope.ServiceProvider.GetRequiredService<IDashboardPlanner>();
                var healthSyncService = plannerScope.ServiceProvider.GetService<IHealthSyncService>();
                HealthWeeklySummary? healthSummary = null;
                if (healthSyncService is not null)
                {
                    var healthStatus = await healthSyncService.GetStatusAsync(userId);
                    if (healthStatus.IsConnected && healthStatus.SelectedSource is { } selectedSource &&
                        healthStatus.LastSyncedAt < DateTime.UtcNow.AddHours(-3))
                        await healthSyncService.SyncAsync(userId, selectedSource);
                    healthSummary = await healthSyncService.GetCachedWeeklySummaryAsync(userId);
                }
                ApplySupportSummary(dashboardPlanner.BuildSupportSummary(
                    prediction,
                    CurrentPhase,
                    CaloriesProgress,
                    profile?.Weight,
                    profile?.WorkoutDaysPerWeek ?? -1,
                    workoutSummary,
                    healthSummary: healthSummary,
                    recordedReadinessScore: readinessLog?.ReadinessScore,
                    hasActiveInjury: HasActiveInjuries,
                    localSteps: localSteps,
                    localAverageSleepHours: localAverageSleepHours));
            }

            NotifyMacroProperties();
            NotifyUserLinkedProperties();

            _calendarUserId = userId;
            _calendarPrediction = prediction;
            await BuildCalendarAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task<T> RunScopedAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        return ScopedServiceRunner.RunAsync(_scopeFactory, operation);
    }

    private void NotifyPhaseProperties()
    {
        OnPropertyChanged(nameof(DashboardPhaseHeaderText));
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseCardTitle));
        OnPropertyChanged(nameof(HormonalSummary));
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
        MacroProgressFormatter.NotifyAll(OnPropertyChanged);
    }

    private void NotifyUserLinkedProperties()
    {
        OnPropertyChanged(nameof(Greetings));
        OnPropertyChanged(nameof(PhaseShortAdvice));
    }

    private void ApplyWorkoutSummary(TodaysWorkoutSummary workoutSummary)
    {
        _todaysWorkoutDayId = workoutSummary.WorkoutDayId;
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
        OnPropertyChanged(nameof(CompositeScore));
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
            : PhaseThemePalette.GetNeutralCardBackgroundColor(UseDarkTheme);
        PhaseCardTextColor = UsePhaseCardColor
            ? GetPhaseTextColor(CurrentPhase, UseDarkTheme)
            : PhaseThemePalette.GetNeutralCardTextColor(UseDarkTheme);
        PhaseCardDividerColor = UsePhaseCardColor
            ? GetPhaseDividerColor(CurrentPhase, UseDarkTheme)
            : PhaseThemePalette.GetNeutralDividerColor(UseDarkTheme);
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
        return PhaseThemePalette.GetPhaseBackgroundColor(phase, useDarkTheme, PhaseThemePalette.GetNeutralCardBackgroundColor(useDarkTheme));
    }

    private static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseTextColor(phase, useDarkTheme, PhaseThemePalette.GetNeutralCardTextColor(useDarkTheme));
    }

    private static Color GetPhaseDividerColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseDividerColor(phase, useDarkTheme);
    }

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        if (_isBulkLoading) return;
        RefreshPhaseCardColors();
        NotifyPhaseProperties();
    }

    partial void OnUsePhaseCardColorChanged(bool value)
    {
        if (_isBulkLoading) return;
        RefreshPhaseCardColors();
    }

    partial void OnUseDarkThemeChanged(bool value)
    {
        RefreshPhaseCardColors();
    }

    partial void OnConsumedCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesConsumed(OnPropertyChanged);
    }

    partial void OnTargetCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesGoal(OnPropertyChanged);
    }

    partial void OnDisplayNameChanged(string value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(Greetings));
    }

    partial void OnCurrentCycleDayChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
    }

    partial void OnPredictedCycleLengthChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
    }

    partial void OnDaysUntilPeriodChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(NextPeriodValue));
    }

    partial void OnCycleInsightTextChanged(string value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(PhaseShortAdvice));
    }

    partial void OnSessionProgressTextChanged(string value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(WorkoutBadgeText));
        OnPropertyChanged(nameof(IsTodaysWorkoutCompleted));
    }

    partial void OnWorkoutStreakDaysChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(WorkoutStreakText));
    }

    partial void OnNutritionStreakDaysChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(NutritionStreakText));
    }

    partial void OnNeedsDailyCheckInChanged(bool value)
    {
        OpenDailyCheckInCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ScoreSourceLabel));
    }
}
