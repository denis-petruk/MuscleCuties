using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Models.UI.Dashboard;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using IPageLoadAware = MuscleCuties.UI.ViewModels.Common.IPageLoadAware;

namespace MuscleCuties.UI.ViewModels.Dashboard;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global",
    Justification = "Bound in XAML / view model contract.")]
public partial class DashboardViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly IWorkoutInjuryUseCase _injuries;
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _openCycleAsync;
    private readonly Func<Task> _openDailyCheckInAsync;
    private readonly Func<Task> _openNutritionAsync;
    private readonly Func<Task>? _openPhotoVaultAsync;
    private readonly Func<Task> _openWorkoutAsync;
    private readonly Func<int, Task> _openWorkoutSessionAsync;

    private readonly IDashboardOverviewUseCase _overview;
    private readonly ICyclePhaseProjectionUseCase _cyclePhaseProjection;
    private readonly DailyCheckInPresentationSettings _readinessPresentationSettings;
    private readonly IWorkoutOverviewUseCase? _workoutOverview;
    private ReadinessAdjustmentProposalDto? _pendingReadinessProposal;
    [ObservableProperty] private float _consumedCalories;
    [ObservableProperty] private float _consumedCarbs;
    [ObservableProperty] private float _consumedFats;
    [ObservableProperty] private float _consumedProtein;
    [ObservableProperty] private int _currentCycleDay;

    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;
    [ObservableProperty] private int _cycleCount;
    [ObservableProperty] private string _cycleInsightText = string.Empty;
    [ObservableProperty] private int _daysUntilPeriod;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private bool _hasActiveCycle;
    [ObservableProperty] private bool _hasActiveInjuries;
    [ObservableProperty] private bool _hasMultipleActivities;
    [ObservableProperty] private bool _hasReadinessTrend;
    [ObservableProperty] private bool _hasTodayPhoto;
    [ObservableProperty] private bool _hasWeightTrend;
    [ObservableProperty] private string _hydrationConsumed = "2.5 L";
    [ObservableProperty] private string _hydrationGoal = "target";

    private InjuryLogViewModel? _injuryLogVm;

    // Suppresses per-field partial On*Changed notifications during a bulk load so that
    // computed properties are only invalidated once at the end via the Notify* helpers.
    private bool _isBulkLoading;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isInjuryModalVisible;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isRestDay = true;
    [ObservableProperty] private bool _needsDailyCheckIn;
    [ObservableProperty] private int _nutritionStreakDays;
    [ObservableProperty] private Color _phaseCardBackgroundColor = AppThemeResources.GetColor("AccentSurface", default);
    [ObservableProperty] private Color _phaseCardDividerColor = AppThemeResources.GetColor("InputStroke", default);
    [ObservableProperty] private Color _phaseCardTextColor = AppThemeResources.GetColor("DashboardPhaseCardTextLight", default);
    [ObservableProperty] private string _photoButtonText = "Check in";
    [ObservableProperty] private string _photoCheckInText = "Add today's progress photo";
    [ObservableProperty] private int _predictedCycleLength = 28;
    [ObservableProperty] private string _readinessLabel = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadinessProgress), nameof(CompositeScore))]
    private int _readinessScore;
    [ObservableProperty] private string _readinessTrendSummary = "Complete a check-in to see readiness history";
    [ObservableProperty] private bool _hasTodayReadinessScore;
    [ObservableProperty] private bool _hasTodayReadinessRecord;
    [ObservableProperty] private bool _todayReadinessReviewPending;
    [ObservableProperty] private bool _todayReadinessIncomplete;
    [ObservableProperty] private string _todayReadinessScoreText = string.Empty;
    [ObservableProperty] private string _todayReadinessBandText = string.Empty;
    [ObservableProperty] private string _todayReadinessGuidanceText = string.Empty;
    [ObservableProperty] private string _todayReadinessSleepQualityText = "—";
    [ObservableProperty] private string _todayReadinessEnergyText = "—";
    [ObservableProperty] private string _todayReadinessSorenessText = "—";
    [ObservableProperty] private string _todayReadinessStressText = "—";
    [ObservableProperty] private bool _canReviewReadiness;
    [ObservableProperty] private bool _hasReadinessProposalActions;
    [ObservableProperty] private bool _isReadinessProposalBusy;
    [ObservableProperty] private bool _isReadinessProposalVisible;
    [ObservableProperty] private string _readinessProposalSummary = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _readinessProposalChanges = [];
    [ObservableProperty] private string _recoveryLabel = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecoveryProgress), nameof(CompositeScore))]
    private int _recoveryScore;
    [ObservableProperty] private string _sessionProgressText = "Upcoming";
    [ObservableProperty] private string _sleepGoal = "8h";
    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private float _targetProtein;
    private int _todaysWorkoutDayId;
    [ObservableProperty] private IReadOnlyList<DashboardTrendPoint> _trendPoints = [];
    [ObservableProperty] private bool _useDarkTheme;
    [ObservableProperty] private bool _usePhaseCardColor;
    [ObservableProperty] private string _weightTrendSummary = "Add weight at three check-ins to see your trend";

    [ObservableProperty] private Color _workoutActivityBackground =
        WorkoutPresentationPalette.Background("REST");

    [ObservableProperty] private ObservableCollection<WorkoutActivitySection> _workoutActivitySections = [];

    [ObservableProperty] private Color _workoutActivityTextColor =
        WorkoutPresentationPalette.Text("REST");

    [ObservableProperty] private string _workoutDurationText = "Rest day";
    [ObservableProperty] private string _workoutExercisesCount = "0";
    [ObservableProperty] private string _workoutIntensity = "Low";
    [ObservableProperty] private int _workoutStreakDays;
    [ObservableProperty] private string _workoutSubtitle = "Recovery day";
    [ObservableProperty] private string _workoutTitle = "Living happy life";

    public DashboardViewModel(
        IDashboardOverviewUseCase overview,
        IWorkoutInjuryUseCase injuries,
        IDailyCheckInUseCase dailyCheckIn,
        ICyclePhaseProjectionUseCase cyclePhaseProjection,
        Func<Task> openCycleAsync,
        Func<Task> openWorkoutAsync,
        Func<int, Task> openWorkoutSessionAsync,
        Func<Task> openNutritionAsync,
        Func<Task> openDailyCheckInAsync,
        INutritionChangeNotifier? changeNotifier = null,
        Func<Task>? openPhotoVaultAsync = null,
        IWorkoutOverviewUseCase? workoutOverview = null)
    {
        _overview = overview;
        _cyclePhaseProjection = cyclePhaseProjection;
        _readinessPresentationSettings = dailyCheckIn.PresentationSettings;
        _workoutOverview = workoutOverview;
        _injuries = injuries;
        _openCycleAsync = openCycleAsync;
        _openWorkoutAsync = openWorkoutAsync;
        _openWorkoutSessionAsync = openWorkoutSessionAsync;
        _openNutritionAsync = openNutritionAsync;
        _openDailyCheckInAsync = openDailyCheckInAsync;
        _openPhotoVaultAsync = openPhotoVaultAsync;
        if (changeNotifier is not null)
            changeNotifier.NutritionChanged += OnNutritionChanged;
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCycleCommand = new AsyncRelayCommand(_openCycleAsync);
        OpenWorkoutCommand = new AsyncRelayCommand(_openWorkoutAsync);
        StartWorkoutCommand = new AsyncRelayCommand(StartWorkoutAsync);
        OpenNutritionCommand = new AsyncRelayCommand(_openNutritionAsync);
        OpenDailyCheckInCommand = new AsyncRelayCommand(_openDailyCheckInAsync);
        ReviewReadinessAdjustmentCommand = new AsyncRelayCommand(ReviewReadinessAdjustmentAsync);
        ApplyReadinessAdjustmentCommand = new AsyncRelayCommand(() => DecideReadinessAdjustmentAsync(true));
        KeepWorkoutPlanCommand = new AsyncRelayCommand(() => DecideReadinessAdjustmentAsync(false));
        OpenPhotoVaultCommand = new AsyncRelayCommand(OpenPhotoVaultAsync);
    }

    public AsyncRelayCommand OpenPhotoVaultCommand { get; }
    public AsyncRelayCommand ReviewReadinessAdjustmentCommand { get; }
    public AsyncRelayCommand ApplyReadinessAdjustmentCommand { get; }
    public AsyncRelayCommand KeepWorkoutPlanCommand { get; }

    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _injuries, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    public string DashboardPhaseHeaderText => CurrentPhase == CyclePhase.Unknown
        ? "This week · Set your cycle phase"
        : $"This week · {CurrentPhase}";

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

    public string PhaseStatusText => CurrentPhase == CyclePhase.Unknown
        ? "Add a cycle phase to get started"
        : CurrentCycleDay > 0
            ? $"{CurrentPhase} · Day {CurrentCycleDay} / {PredictedCycleLength}"
            : $"{CurrentPhase} · Phase selected";

    public string WorkoutCountLabel => IsRestDay
        ? "Activity"
        : WorkoutExercisesCount.Contains("sets", StringComparison.OrdinalIgnoreCase)
            ? "Sets"
            : "Exercises";

    public string WorkoutCountValue => IsRestDay
        ? "Recovery"
        : int.TryParse(WorkoutExercisesCount.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            out var count)
            ? count.ToString(CultureInfo.CurrentCulture)
            : WorkoutExercisesCount;

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
        CyclePhase.Menstrual => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            "Take it easy, prioritize rest"),
        CyclePhase.Follicular => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            "Energy rising, great time to train"),
        CyclePhase.Ovulatory => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            "Peak energy and strength"),
        CyclePhase.Luteal => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            "Moderate intensity, listen to your body"),
        _ => string.Empty
    };

    public string PhaseIconSource => CyclePhaseAssets.GetIconSource(CurrentPhase);
    public bool HasKnownPhase => CurrentPhase != CyclePhase.Unknown;

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
    public string NextPeriodValue => !HasActiveCycle && CurrentCycleDay <= 0
        ? "--"
        : DaysUntilPeriod <= 0 ? "Today" : $"{DaysUntilPeriod}d";

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

    private const float MaximumScoreProgressDivisor = 100f;
    private const int MinimumScoreValue = 0;
    private const int MaximumScoreValue = 100;
    private const int MinimumRatingScale = 1;
    private const int MaximumRatingScale = 5;
    private const double CheckInRecoveryScaleRange = 8.0;
    private const double CheckInRecoveryWeight = 0.7;
    private const double BaselineRecoveryWeight = 0.3;

    public int CompositeScore => (ReadinessScore + RecoveryScore) / 2;

    public string ReadinessSubtitleText => HasTodayReadinessRecord
        ? "Dashboard_Readiness_SelfReportedSubtitle"
        : "Dashboard_Readiness_HealthOrBaselineSubtitle";

    public float ReadinessProgress =>
        Math.Clamp(ReadinessScore / MaximumScoreProgressDivisor, 0f, 1f);

    public float RecoveryProgress =>
        Math.Clamp(RecoveryScore / MaximumScoreProgressDivisor, 0f, 1f);

    public bool HasAnyTrend => HasWeightTrend;

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
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand OpenCycleCommand { get; }
    public AsyncRelayCommand OpenWorkoutCommand { get; }
    public AsyncRelayCommand StartWorkoutCommand { get; }
    public AsyncRelayCommand OpenNutritionCommand { get; }
    public AsyncRelayCommand OpenDailyCheckInCommand { get; }

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public AsyncRelayCommand LoadDataCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    private Task OpenPhotoVaultAsync()
    {
        return _openPhotoVaultAsync?.Invoke() ?? Task.CompletedTask;
    }

    private void OnNutritionChanged(object? sender, NutritionChangedEventArgs e)
    {
        _loadGate.InvalidateAndReloadIfToday(e.Date, LoadDataCoreAsync, this);
    }

    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    public Task OnProfileChangedAsync()
    {
        Invalidate();
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    public void OnProfileChanged()
    {
        _ = OnProfileChangedAsync();
    }

    public Task OnCycleChangedAsync()
    {
        Invalidate();
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    public void OnCycleChanged()
    {
        _ = OnCycleChangedAsync();
    }

    [RelayCommand]
    private async Task OpenInjuryModalAsync()
    {
        InjuryLogVm.ResetForm();
        IsInjuryModalVisible = true;
        if (!InjuryLogVm.IsBusy)
            await InjuryLogVm.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void CloseInjuryModal()
    {
        _injuryLogVm?.ResetForm();
        IsInjuryModalVisible = false;
    }

    private async Task RefreshAfterInjuryChangeAsync()
    {
        HasActiveInjuries = InjuryLogVm.HasActiveInjuries;
        Invalidate();
        await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

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

    private Task StartWorkoutAsync()
    {
        return IsRestDay || _todaysWorkoutDayId <= 0
            ? _openWorkoutAsync()
            : _openWorkoutSessionAsync(_todaysWorkoutDayId);
    }

    private async Task LoadDataCoreAsync()
    {
        IsReadinessProposalVisible = false;
        HasReadinessProposalActions = false;
        _pendingReadinessProposal = null;
        IsBusy = true;
        IsLoadError = false;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var timeZoneId = TimeZoneInfo.Local.Id;
            var summary = await _overview.LoadAsync(new DashboardOverviewRequest(today, timeZoneId));
            _isBulkLoading = true;
            DisplayName = summary.DisplayName;
            HasActiveInjuries = summary.HasActiveInjuries;
            HasActiveCycle = summary.HasActiveCycle;
            CurrentPhase = Enum.TryParse<CyclePhase>(summary.CurrentPhase, true, out var phase) && Enum.IsDefined(phase)
                ? phase
                : CyclePhase.Unknown;
            CurrentCycleDay = Math.Max(summary.CurrentCycleDay, 0);
            PredictedCycleLength = Math.Clamp(summary.PredictedCycleLength, 21, 45);
            DaysUntilPeriod = Math.Max(summary.DaysUntilPeriod, 0);
            CycleCount = Math.Max(summary.CycleCount, 0);
            UsePhaseCardColor = summary.HasActiveCycle ||
                                summary.PredictionSource.Equals("manual phase log",
                                    StringComparison.OrdinalIgnoreCase) ||
                                summary.PredictionSource.Equals("profile phase", StringComparison.OrdinalIgnoreCase);
            ConsumedCalories = summary.ConsumedCalories;
            ConsumedProtein = summary.ConsumedProtein;
            ConsumedCarbs = summary.ConsumedCarbs;
            ConsumedFats = summary.ConsumedFats;
            TargetCalories = summary.TargetCalories;
            TargetProtein = summary.TargetProtein;
            TargetCarbs = summary.TargetCarbs;
            TargetFats = summary.TargetFats;
            WorkoutStreakDays = Math.Max(summary.WorkoutStreakDays, 0);
            NutritionStreakDays = Math.Max(summary.NutritionStreakDays, 0);
            HasTodayPhoto = summary.HasTodayPhoto;
            PhotoCheckInText = HasTodayPhoto ? "Progress photo logged for today" : "Add today's progress photo";
            PhotoButtonText = HasTodayPhoto ? "View vault" : "Check in";
            CycleInsightText = summary.CycleInsightText;
            HydrationConsumed = summary.HydrationConsumed;
            HydrationGoal = summary.HydrationGoal;
            SleepGoal = summary.SleepGoal;
            ReadinessScore = summary.ReadinessScore;
            ReadinessLabel = summary.ReadinessLabel;
            RecoveryScore = summary.RecoveryScore;
            RecoveryLabel = summary.RecoveryLabel;
            ApplyTodayReadiness(summary.TodayReadiness);
            NeedsDailyCheckIn = summary.TodayReadiness is not { Status: "Available" };
            TrendPoints = summary.TrendPoints.Select(point =>
                new DashboardTrendPoint(point.Date, point.WeightKg, point.ReadinessScore)).ToArray();
            HasWeightTrend = TrendPoints.Any(point => point.WeightKg.HasValue);
            HasReadinessTrend = TrendPoints.Any(point => point.ReadinessScore.HasValue);
            var lastWeight = TrendPoints.LastOrDefault(point => point.WeightKg.HasValue)?.WeightKg;
            WeightTrendSummary = lastWeight is null
                ? "Add weight at three check-ins to see your trend"
                : $"7-day median {lastWeight.Value:N1} kg";
            ReadinessTrendSummary = HasReadinessTrend
                ? $"{TrendPoints.Count(point => point.ReadinessScore.HasValue)} of 7 days checked in"
                : "Complete a check-in to see readiness history";
            OnPropertyChanged(nameof(HasAnyTrend));
            ApplyTrendSummaries(summary);
            _todaysWorkoutDayId = summary.WorkoutDayId;
            WorkoutTitle = summary.WorkoutTitle;
            WorkoutSubtitle = summary.WorkoutSubtitle;
            WorkoutDurationText = summary.WorkoutDurationText;
            WorkoutExercisesCount = summary.WorkoutExercisesCount;
            WorkoutIntensity = summary.WorkoutIntensity;
            SessionProgressText = summary.SessionProgressText;
            WorkoutActivityBackground = WorkoutPresentationPalette.Background(summary.WorkoutActivityTag);
            WorkoutActivityTextColor = WorkoutPresentationPalette.Text(summary.WorkoutActivityTag);
            IsRestDay = summary.WorkoutActivityTag.Equals("REST", StringComparison.OrdinalIgnoreCase) ||
                        summary.WorkoutActivityTag.Equals("Recovery", StringComparison.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(WorkoutCountLabel));
            OnPropertyChanged(nameof(WorkoutCountValue));
            WorkoutActivitySections = new ObservableCollection<WorkoutActivitySection>(
                summary.WorkoutActivitySections.Select(section => new WorkoutActivitySection(
                    section.Tag, section.Title, section.ExerciseCount,
                    WorkoutPresentationPalette.Background(section.Tag), WorkoutPresentationPalette.Text(section.Tag))));
            HasMultipleActivities = WorkoutActivitySections.Count > 1;
            _isBulkLoading = false;
            RefreshPhaseCardColors();
            NotifyPhaseProperties();
            NotifyUserLinkedProperties();
            NotifyMacroProperties();
            OnPropertyChanged(nameof(ReadinessProgress));
            OnPropertyChanged(nameof(RecoveryProgress));
            OnPropertyChanged(nameof(CompositeScore));
        }
        catch (Exception ex)
        {
            _isBulkLoading = false;
            IsLoadError = true;
            Trace.WriteLine($"[Dashboard] Load failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyTodayReadiness(TodayReadinessCheckInDto? readiness)
    {
        HasTodayReadinessRecord = readiness is not null;
        HasTodayReadinessScore = _readinessPresentationSettings.IsReadinessScoreDisplayEnabled &&
                                 readiness is { Status: "Available", RawScore: { } };
        TodayReadinessReviewPending = readiness is { Status: "Available" } &&
                                     !_readinessPresentationSettings.IsReadinessScoreDisplayEnabled;
        TodayReadinessIncomplete = readiness?.Status == "Incomplete";
        if (readiness is { Status: "Available" })
        {
            if (readiness.RawScore is { } rawScore)
            {
                ReadinessScore = (int)Math.Round(
                    Math.Clamp((double)rawScore, MinimumScoreValue, MaximumScoreValue),
                    MidpointRounding.AwayFromZero);
            }

            if (readiness.SleepQuality is { } sq && readiness.Soreness is { } s)
            {
                var checkInRecovery = ((sq - MinimumRatingScale) + (MaximumRatingScale - s)) *
                                      MaximumScoreValue / CheckInRecoveryScaleRange;
                RecoveryScore = (int)Math.Round(
                    Math.Clamp(checkInRecovery * CheckInRecoveryWeight + RecoveryScore * BaselineRecoveryWeight,
                        MinimumScoreValue, MaximumScoreValue),
                    MidpointRounding.AwayFromZero);
            }
        }

        TodayReadinessScoreText = HasTodayReadinessScore
            ? readiness!.RawScore!.Value.ToString("0.##", CultureInfo.CurrentCulture)
            : string.Empty;
        TodayReadinessBandText = readiness?.Band switch
        {
            "FeelingReady" => "Dashboard_Readiness_Band_FeelingReady",
            "ConsiderLighterSession" => "Dashboard_Readiness_Band_ConsiderLighterSession",
            "ConsiderEasierDay" => "Dashboard_Readiness_Band_ConsiderEasierDay",
            _ => string.Empty
        };
        TodayReadinessGuidanceText = readiness?.Band switch
        {
            "FeelingReady" => "Dashboard_Readiness_Guidance_FeelingReady",
            "ConsiderLighterSession" => "Dashboard_Readiness_Guidance_LighterSession",
            "ConsiderEasierDay" => "Dashboard_Readiness_Guidance_EasierDay",
            _ => string.Empty
        };
        TodayReadinessSleepQualityText = FormatRating(readiness?.SleepQuality);
        TodayReadinessEnergyText = FormatRating(readiness?.Energy);
        TodayReadinessSorenessText = FormatRating(readiness?.Soreness);
        TodayReadinessStressText = FormatRating(readiness?.Stress);
        OnPropertyChanged(nameof(ReadinessSubtitleText));
        CanReviewReadiness = _readinessPresentationSettings.AreReadinessSuggestionsEnabled &&
                             HasTodayReadinessScore && !HasActiveInjuries &&
                             readiness?.Band is "ConsiderLighterSession" or "ConsiderEasierDay";
    }

    private async Task ReviewReadinessAdjustmentAsync()
    {
        if (_workoutOverview is null || !CanReviewReadiness) return;
        IsReadinessProposalBusy = true;
        try
        {
            var date = DateOnly.FromDateTime(DateTime.Today);
            _pendingReadinessProposal = await _workoutOverview.ReviewReadinessAdjustmentAsync(
                new ReviewReadinessAdjustmentRequest(date, TimeZoneInfo.Local.Id));
            ReadinessProposalSummary = _pendingReadinessProposal.Summary;
            ReadinessProposalChanges = new ObservableCollection<string>(
                _pendingReadinessProposal.Changes.Select(change => change.Description));
            HasReadinessProposalActions = _pendingReadinessProposal.CanApply &&
                                          _pendingReadinessProposal.ProposalId != Guid.Empty;
            IsReadinessProposalVisible = true;
        }
        catch (Exception ex)
        {
            _pendingReadinessProposal = null;
            HasReadinessProposalActions = false;
            ReadinessProposalSummary = ex.Message;
            ReadinessProposalChanges.Clear();
            IsReadinessProposalVisible = true;
        }
        finally
        {
            IsReadinessProposalBusy = false;
        }
    }

    private async Task DecideReadinessAdjustmentAsync(bool apply)
    {
        if (_workoutOverview is null || _pendingReadinessProposal is not { ProposalId: var id } || id == Guid.Empty)
            return;
        IsReadinessProposalBusy = true;
        var successMessage = string.Empty;
        try
        {
            var request = new DecideReadinessAdjustmentRequest(id,
                DateOnly.FromDateTime(DateTime.Today), TimeZoneInfo.Local.Id);
            var result = await _workoutOverview.DecideReadinessAdjustmentAsync(request, apply);
            ReadinessProposalSummary = result.Message;
            HasReadinessProposalActions = false;
            _pendingReadinessProposal = null;
            ReadinessProposalChanges.Clear();
            if (result.Applied)
            {
                successMessage = result.Message;
                await RefreshAsync();
                ReadinessProposalSummary = successMessage;
                IsReadinessProposalVisible = true;
            }
        }
        catch (Exception ex)
        {
            ReadinessProposalSummary = ex.Message;
            HasReadinessProposalActions = false;
            _pendingReadinessProposal = null;
        }
        finally
        {
            IsReadinessProposalBusy = false;
        }
    }

    private static string FormatRating(int? rating) => rating?.ToString(CultureInfo.CurrentCulture) ?? "—";

    private void NotifyPhaseProperties()
    {
        OnPropertyChanged(nameof(DashboardPhaseHeaderText));
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseCardTitle));
        OnPropertyChanged(nameof(HormonalSummary));
        OnPropertyChanged(nameof(PhaseShortAdvice));
        OnPropertyChanged(nameof(PhaseIconSource));
        OnPropertyChanged(nameof(HasKnownPhase));
        OnPropertyChanged(nameof(CurrentPhaseColumn));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
        OnPropertyChanged(nameof(PhaseTimeLeftLabel));
        OnPropertyChanged(nameof(NextPeriodValue));
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


    private int CalculateDaysLeftInCurrentPhase()
    {
        return _cyclePhaseProjection.GetDaysRemaining(
            new CyclePhaseCountdownInputDto(CurrentCycleDay, PredictedCycleLength, CurrentPhase)).DaysRemaining;
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

    private static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseBackgroundColor(phase, useDarkTheme,
            PhaseThemePalette.GetNeutralCardBackgroundColor(useDarkTheme));
    }

    private static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseTextColor(phase, useDarkTheme,
            PhaseThemePalette.GetNeutralCardTextColor(useDarkTheme));
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
        OnPropertyChanged(nameof(NextPeriodValue));
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
    }
}
