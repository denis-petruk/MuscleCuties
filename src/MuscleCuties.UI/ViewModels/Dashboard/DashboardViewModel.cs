using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Graphics;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using IHealthSyncService = MuscleCuties.Core.Services.Health.IHealthSyncService;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Models.UI.Dashboard;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using IPageLoadAware = MuscleCuties.UI.ViewModels.Common.IPageLoadAware;

namespace MuscleCuties.UI.ViewModels.Dashboard;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global",
    Justification = "Bound in XAML / view model contract.")]
/// <summary>The dashboard: today's readiness, the workout and cycle summaries, and the two sheets it hosts — the daily</summary>
/// check-in and the injury log.</summary>
public partial class DashboardViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly IDailyCheckInUseCase _dailyCheckIn;
    private readonly IWorkoutInjuryUseCase _injuries;
    private readonly Action _invalidateDashboard;
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _openCycleAsync;
    private readonly Func<Task> _openNutritionAsync;
    private readonly Func<Task>? _openPhotoVaultAsync;
    private readonly Func<Task> _openWorkoutAsync;
    private readonly Func<int, Task> _openWorkoutSessionAsync;

    private readonly IDashboardOverviewUseCase _overview;
    private readonly ICyclePhaseProjectionUseCase _cyclePhaseProjection;
    private readonly IProfileUnitsUseCase _profileUnits;
    private readonly IHealthSyncService? _healthSync;
    private readonly ICurrentProfileUserIdProvider? _userIdProvider;

    // F06: the dashboard renders weights and distances in the saved measurement system.
    private bool _useMetricUnits = true;
    private readonly DailyCheckInPresentationSettings _readinessPresentationSettings;
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

    private DailyCheckInViewModel? _checkInVm;
    private InjuryLogViewModel? _injuryLogVm;

    // Suppresses per-field partial On*Changed notifications during a bulk load so that
    // computed properties are only invalidated once at the end via the Notify* helpers.
    private bool _isBulkLoading;
    private MuscleCuties.UI.Services.Api.ApiFailureNotice? _lastFailure;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyModalVisible))]
    private bool _isCheckInModalVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyModalVisible))]
    private bool _isInjuryModalVisible;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isRestDay = true;
    [ObservableProperty] private bool _needsDailyCheckIn;
    [ObservableProperty] private int _nutritionStreakDays;
    [ObservableProperty] private Color _phaseCardBackgroundColor = AppThemeResources.GetColor("AccentSurface", default);
    [ObservableProperty] private Color _phaseCardDividerColor = AppThemeResources.GetColor("InputStroke", default);
    [ObservableProperty] private Color _phaseCardTextColor = AppThemeResources.GetColor("DashboardPhaseCardTextLight", default);
    [ObservableProperty] private string _photoButtonText =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Photo_CheckInAction");
    [ObservableProperty] private string _photoCheckInText =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Photo_AddToday");
    private bool _isCycleLengthEstimated;
    [ObservableProperty] private int _predictedCycleLength = 28;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadinessProgress), nameof(CompositeScore))]
    private int _readinessScore;
    [ObservableProperty] private string _readinessTrendSummary =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Readiness_TrendEmpty");
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTodayReadinessGuidance))]
    private bool _hasTodayReadinessScore;
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
    [ObservableProperty] private string _recoveryLabel = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecoveryProgress), nameof(CompositeScore))]
    private int _recoveryScore;
    [ObservableProperty] private string _sessionProgressText =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Workout_Overview_Status_Upcoming");
    [ObservableProperty] private string _sleepGoal = "8h";
    [ObservableProperty] private float _targetCalories;
    [ObservableProperty] private float _targetCarbs;
    [ObservableProperty] private float _targetFats;
    [ObservableProperty] private float _targetProtein;
    private int _todaysWorkoutDayId;
    [ObservableProperty] private IReadOnlyList<DashboardTrendPoint> _trendPoints = [];
    [ObservableProperty] private bool _useDarkTheme;
    [ObservableProperty] private bool _usePhaseCardColor;
    [ObservableProperty] private string _weightTrendSummary =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Weight_TrendEmpty");

    [ObservableProperty] private Color _workoutActivityBackground =
        WorkoutPresentationPalette.Background("REST");

    [ObservableProperty] private ObservableCollection<WorkoutActivitySection> _workoutActivitySections = [];

    [ObservableProperty] private Color _workoutActivityTextColor =
        WorkoutPresentationPalette.Text("REST");

    [ObservableProperty] private string _workoutDurationText =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_RestDay");
    [ObservableProperty] private string _workoutExercisesCount = "0";
    [ObservableProperty] private string _workoutIntensity = "Low";
    [ObservableProperty] private int _workoutStreakDays;
    [ObservableProperty] private string _workoutSubtitle =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_RecoverySubtitle");
    [ObservableProperty] private string _workoutTitle =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Workout_Page_PlaceholderTitle");

    /// <summary>Takes the use cases and the callbacks the dashboard reports to.</summary>
    public DashboardViewModel(
        IDashboardOverviewUseCase overview,
        IWorkoutInjuryUseCase injuries,
        IDailyCheckInUseCase dailyCheckIn,
        ICyclePhaseProjectionUseCase cyclePhaseProjection,
        IProfileUnitsUseCase profileUnits,
        Func<Task> openCycleAsync,
        Func<Task> openWorkoutAsync,
        Func<int, Task> openWorkoutSessionAsync,
        Func<Task> openNutritionAsync,
        Action invalidateDashboard,
        INutritionChangeNotifier? changeNotifier = null,
        Func<Task>? openPhotoVaultAsync = null,
        IHealthSyncService? healthSync = null,
        ICurrentProfileUserIdProvider? userIdProvider = null,
        IProfilePersonalInfoUseCase? personalInfo = null)
    {
        _personalInfo = personalInfo;
        _overview = overview;
        _cyclePhaseProjection = cyclePhaseProjection;
        _profileUnits = profileUnits;
        _readinessPresentationSettings = dailyCheckIn.PresentationSettings;
        _dailyCheckIn = dailyCheckIn;
        _injuries = injuries;
        _openCycleAsync = openCycleAsync;
        _openWorkoutAsync = openWorkoutAsync;
        _openWorkoutSessionAsync = openWorkoutSessionAsync;
        _openNutritionAsync = openNutritionAsync;
        _invalidateDashboard = invalidateDashboard;
        _openPhotoVaultAsync = openPhotoVaultAsync;
        _healthSync = healthSync;
        _userIdProvider = userIdProvider;
        if (changeNotifier is not null)
            changeNotifier.NutritionChanged += OnNutritionChanged;
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCycleCommand = new AsyncRelayCommand(_openCycleAsync);
        OpenWorkoutCommand = new AsyncRelayCommand(_openWorkoutAsync);
        StartWorkoutCommand = new AsyncRelayCommand(StartWorkoutAsync);
        OpenNutritionCommand = new AsyncRelayCommand(_openNutritionAsync);
        OpenPhotoVaultCommand = new AsyncRelayCommand(OpenPhotoVaultAsync);
    }

    /// <summary>Opens the photo vault.</summary>
    public AsyncRelayCommand OpenPhotoVaultCommand { get; }

    /// <summary>True when any dashboard modal is open.</summary>
    public bool IsAnyModalVisible => IsInjuryModalVisible || IsCheckInModalVisible;

    /// <summary>True when readiness guidance is shown today.</summary>
    public bool HasTodayReadinessGuidance => HasTodayReadinessScore &&
                                             _readinessPresentationSettings.AreReadinessSuggestionsEnabled;

    /// <summary>The check-in sheet's model, built on first use so the dashboard does not construct a form nobody opened.</summary>
    public DailyCheckInViewModel CheckInVm => _checkInVm ??= new DailyCheckInViewModel(
        _dailyCheckIn, _invalidateDashboard, CloseCheckInAfterCompletionAsync, _healthSync, _userIdProvider);

    /// <summary>The injury sheet's model, likewise built on first use; its back action is deliberately empty because the sheet</summary>
    /// closes itself.</summary>
    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _injuries, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    /// <summary>The phase card's heading, which says the phase is missing instead of leaving the row blank when there is</summary>
    /// none.</summary>
    public string DashboardPhaseHeaderText => CurrentPhase == CyclePhase.Unknown
        ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Phase_MissingHeader")
        : MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
            "Dashboard_Phase_HeaderFormat", LocalizedCurrentPhaseName);

    /// <summary>Typical hormone patterns for the phase, worded as typical and replaced by a prompt to log a cycle when there</summary>
    /// is none: it is education, not a measurement of her hormones.</summary>
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

    /// <summary>Which day of the cycle this is when it can be counted, otherwise the phase's name.</summary>
    public string PhaseStatusText => CurrentPhase == CyclePhase.Unknown
        ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Phase_MissingStatus")
        : CurrentCycleDay > 0
            ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
                _isCycleLengthEstimated
                    ? "Dashboard_Phase_StatusDayEstimateFormat"
                    : "Dashboard_Phase_StatusDayFormat",
                LocalizedCurrentPhaseName,
                CurrentCycleDay.ToString(CultureInfo.CurrentCulture),
                PredictedCycleLength.ToString(CultureInfo.CurrentCulture))
            : MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
                "Dashboard_Phase_StatusSelectedFormat", LocalizedCurrentPhaseName);

    /// <summary>The label above the workout figure names the unit the plan's summary line is counted in, because that</summary>
    /// wording comes from the plan itself.</summary>
    public string WorkoutCountLabel => IsRestDay
        ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_CountActivity")
        : WorkoutExercisesCount.Contains("sets", StringComparison.OrdinalIgnoreCase)
            ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_CountSets")
            : MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_CountExercises");

    /// <summary>The workout figure: the recovery wording on a rest day, otherwise the leading number of the plan's summary</summary>
    /// line.</summary>
    public string WorkoutCountValue => IsRestDay
        ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_CountRecovery")
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

    /// <summary>The phase card's title for the phase she is in.</summary>
    public string PhaseCardTitle => MuscleCuties.UI.Services.Localization.UiTextCatalog.Get(CurrentPhase switch
    {
        CyclePhase.Menstrual => "Dashboard_Phase_TitleRecovery",
        CyclePhase.Follicular => "Dashboard_Phase_TitleBuild",
        CyclePhase.Ovulatory => "Dashboard_Phase_TitlePower",
        CyclePhase.Luteal => "Dashboard_Phase_TitleSteady",
        _ => "Dashboard_Phase_TitleCycle"
    });

    /// <summary>The short advice for the phase, preferring this cycle's own insight and falling back to the generic text.</summary>
    public string PhaseShortAdvice => CurrentPhase switch
    {
        CyclePhase.Menstrual => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Phase_AdviceMenstrual")),
        CyclePhase.Follicular => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Phase_AdviceFollicular")),
        CyclePhase.Ovulatory => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Phase_AdviceOvulatory")),
        CyclePhase.Luteal => FirstPresent(HasActiveCycle ? CycleInsightText : string.Empty,
            MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Phase_AdviceLuteal")),
        _ => string.Empty
    };

    /// <summary>The phase's localized name.</summary>
    private string LocalizedCurrentPhaseName =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get($"Dashboard_Phase_{CurrentPhase}");

    /// <summary>The icon for the current phase.</summary>
    public string PhaseIconSource => CyclePhaseAssets.GetIconSource(CurrentPhase);
    /// <summary>True when the current phase is known.</summary>
    public bool HasKnownPhase => CurrentPhase != CyclePhase.Unknown;

    /// <summary>Which column of the phase strip stands for the phase she is in.</summary>
    public int CurrentPhaseColumn => CurrentPhase switch
    {
        CyclePhase.Menstrual => 0,
        CyclePhase.Follicular => 1,
        CyclePhase.Ovulatory => 2,
        CyclePhase.Luteal => 3,
        _ => 0
    };

    /// <summary>Days left in the current phase, or a placeholder while the cycle day is not known.</summary>
    public string PhaseTimeLeftValue => CurrentCycleDay <= 0 ? "--" : $"{CalculateDaysLeftInCurrentPhase()}d";
    /// <summary>Ovulation is framed as peak time rather than a phase boundary, so its label differs from the other</summary>
    /// phases.</summary>
    public string PhaseTimeLeftLabel => CurrentPhase is CyclePhase.Ovulatory ? "Peak left" : "Phase left";
    /// <summary>Days until the next period, or the prompt to log period dates when there is no cycle to predict from.</summary>
    public string NextPeriodValue => !HasActiveCycle && CurrentCycleDay <= 0
        ? "--"
        : DaysUntilPeriod <= 0 ? "Today" : $"{DaysUntilPeriod}d";

    /// <summary>The phase's training-load note.</summary>
    public string LoadAdjustmentText => CurrentPhase switch
    {
        CyclePhase.Menstrual => "-10%",
        CyclePhase.Follicular => "+3%",
        CyclePhase.Ovulatory => "+5%",
        CyclePhase.Luteal => "-5%",
        _ => "0%"
    };

    /// <summary>The workout badge names the session's state, so the card is not read as untouched once it has been started or</summary>
    /// finished.</summary>
    public string WorkoutBadgeText => IsTodaysWorkoutCompleted
        ? "Workout completed"
        : IsWorkoutInProgress
            ? "Today · In Progress"
            : $"Today · {SessionProgressText}";

    // The store reports "Completed (<volume> kg volume)" for a finished session, so match the prefix.
    /// <summary>True once today's session is logged, which decides both the badge and what the action offers.</summary>
    public bool IsTodaysWorkoutCompleted =>
        SessionProgressText.StartsWith("Completed", StringComparison.OrdinalIgnoreCase);

    // The store reports "Active Session" for a session that has started but not finished.
    /// <summary>True when a session has been started but not yet finished.</summary>
    public bool IsWorkoutInProgress =>
        string.Equals(SessionProgressText, "Active Session", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(SessionProgressText, "In Progress", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(WorkoutSubtitle, "In Progress", StringComparison.OrdinalIgnoreCase);

    // Suppress redundant status-only subtitles already announced by WorkoutBadgeText ("Today · In Progress" / "Workout completed").
    /// <summary>The workout subtitle appears only when there is a session to describe.</summary>
    public bool ShowWorkoutSubtitle =>
        !string.IsNullOrWhiteSpace(WorkoutSubtitle) &&
        !(IsWorkoutInProgress && string.Equals(WorkoutSubtitle.Trim(), "In Progress", StringComparison.OrdinalIgnoreCase)) &&
        !(IsTodaysWorkoutCompleted && string.Equals(WorkoutSubtitle.Trim(), "Completed Today", StringComparison.OrdinalIgnoreCase));

    // One label for the Today primary action, so a finished or running session is never offered as "Start".
    /// <summary>The action reads as starting, resuming or resting, following the session's state.</summary>
    public string WorkoutActionText => IsRestDay
        ? UiTextCatalog.Get("Dashboard_Workout_ViewRestDay")
        : IsTodaysWorkoutCompleted
            ? UiTextCatalog.Get("Dashboard_Workout_ViewCompleted")
            : IsWorkoutInProgress
                ? UiTextCatalog.Get("Workout_Overview_ResumeWorkout")
                : UiTextCatalog.Get("Dashboard_Workout_StartWorkout");

    /// <summary>The injury button's spoken label names what it opens.</summary>
    public string InjuryButtonAccessibilityText => UiTextCatalog.Get(
        HasActiveInjuries ? "Dashboard_Page_OpenInjuryLogActiveA11y" : "Dashboard_Page_OpenInjuryLogA11y");

    /// <summary>The nutrition card's spoken summary, carrying the day's totals so the card is not read as bare labels.</summary>
    public string NutritionCardAccessibilityText => UiTextCatalog.Format(
        "Dashboard_Nutrition_SummaryA11yFormat",
        CaloriesConsumed,
        CaloriesGoal,
        ProteinText,
        CarbsText,
        FatsText);

    /// <summary>The session streak, worded for one day or many.</summary>
    public string WorkoutStreakText => WorkoutStreakDays == 1
        ? "1 day session streak"
        : $"{WorkoutStreakDays} day session streak";

    /// <summary>The logging streak, worded the same way.</summary>
    public string NutritionStreakText => NutritionStreakDays == 1
        ? "1 day log streak"
        : $"{NutritionStreakDays} day log streak";

    private const float MaximumScoreProgressDivisor = 100f;
    private const int MinimumScoreValue = 0;
    private const int MaximumScoreValue = 100;
    private const int MinimumRatingScale = 1;
    // Sleep quality is the only check-in rating in this component; soreness is not scored (A-8).
    private const double CheckInRecoveryScaleRange = 4.0;
    private const double CheckInRecoveryWeight = 0.7;
    private const double BaselineRecoveryWeight = 0.3;

    /// <summary>The single figure the ring shows: her self-reported readiness averaged with the computed recovery, so neither</summary>
    /// one alone can carry the score.</summary>
    public int CompositeScore => (ReadinessScore + RecoveryScore) / 2;

    /// <summary>The subtitle marks the score as self-reported, and only when there is a check-in to report.</summary>
    public string ReadinessSubtitleText => HasTodayReadinessRecord
        ? "Dashboard_Readiness_SelfReportedSubtitle"
        : string.Empty;

    /// <summary>Readiness as a capped 0–1 fraction for the ring.</summary>
    public float ReadinessProgress =>
        Math.Clamp(ReadinessScore / MaximumScoreProgressDivisor, 0f, 1f);

    /// <summary>Recovery as the same capped fraction.</summary>
    public float RecoveryProgress =>
        Math.Clamp(RecoveryScore / MaximumScoreProgressDivisor, 0f, 1f);

    /// <summary>True when at least one trend has data.</summary>
    public bool HasAnyTrend => HasWeightTrend;

    /// <summary>Calories as a capped fraction of the target, so an overshoot does not draw past the bar's end.</summary>
    public float CaloriesProgress =>
        MacroProgressFormatter.CalculateProgress(ConsumedCalories, TargetCalories);

    /// <summary>The day's calories in the card's own number formatting.</summary>
    public string CaloriesConsumed => MacroProgressFormatter.FormatCaloriesConsumed(ConsumedCalories);
    /// <summary>The calorie target in the same formatting, so consumed and goal read as a pair.</summary>
    public string CaloriesGoal => MacroProgressFormatter.FormatCaloriesGoal(TargetCalories);

    /// <summary>Protein consumed against its target, as the macro row shows it.</summary>
    public string ProteinText => MacroProgressFormatter.FormatGramsText(ConsumedProtein, TargetProtein);
    /// <summary>Protein as a capped fraction of its target.</summary>
    public float ProteinProgress => MacroProgressFormatter.CalculateProgress(ConsumedProtein, TargetProtein);

    /// <summary>Carbohydrate consumed against its target.</summary>
    public string CarbsText => MacroProgressFormatter.FormatGramsText(ConsumedCarbs, TargetCarbs);
    /// <summary>Carbohydrate as a capped fraction of its target.</summary>
    public float CarbsProgress => MacroProgressFormatter.CalculateProgress(ConsumedCarbs, TargetCarbs);

    /// <summary>Fat consumed against its target.</summary>
    public string FatsText => MacroProgressFormatter.FormatGramsText(ConsumedFats, TargetFats);
    /// <summary>Fat as a capped fraction of its target.</summary>
    public float FatsProgress => MacroProgressFormatter.CalculateProgress(ConsumedFats, TargetFats);
    /// <summary>Refreshes the dashboard.</summary>
    public AsyncRelayCommand RefreshCommand { get; }
    /// <summary>Opens the cycle page.</summary>
    public AsyncRelayCommand OpenCycleCommand { get; }
    /// <summary>Opens the workout page.</summary>
    public AsyncRelayCommand OpenWorkoutCommand { get; }
    /// <summary>Starts the planned workout.</summary>
    public AsyncRelayCommand StartWorkoutCommand { get; }

    private readonly IProfilePersonalInfoUseCase? _personalInfo;

    /// <summary>The calorie target adjusted for today's active energy from the health store, or the base target unchanged when</summary>
    /// that data cannot be read — an unavailable value never moves the budget.</summary>
    private async Task<float> ApplyHealthCalorieAdjustmentAsync(float baseTarget)
    {
        if (_healthSync is null || _userIdProvider is null || _personalInfo is null)
            return baseTarget;
        try
        {
            var userId = await _userIdProvider.GetCurrentUserIdAsync();
            if (userId <= 0) return baseTarget;
            var healthSummary = await _healthSync.GetCachedWeeklySummaryAsync(userId);
            if (healthSummary is null || !healthSummary.HasMovementData) return baseTarget;

            var info = await _personalInfo.LoadAsync();
            var weightKg = info.Profile is { Weight: > 0 } profile ? profile.Weight : 0f;
            var age = 0;
            if (info.Profile is { } p && p.DateOfBirth != default)
            {
                age = DateTime.Today.Year - p.DateOfBirth.Year;
                if (p.DateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
            }
            // No invented defaults: unknown weight or age means no activity adjustment.
            if (weightKg <= 0f || age <= 0) return baseTarget;

            return baseTarget + MuscleCuties.Core.Services.Health.HealthEnergyAdjustment.CalculateDailyCalories(
                healthSummary, weightKg, age, 3,
                Enum.TryParse<MuscleCuties.Core.Models.Enums.Cycle.CyclePhase>(CurrentPhase.ToString(), out var corePhase)
                    ? corePhase : MuscleCuties.Core.Models.Enums.Cycle.CyclePhase.Unknown);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DashboardViewModel] Calorie adjustment skipped ({ex.GetType().Name}).");
            return baseTarget;
        }
    }
    /// <summary>Opens the nutrition page.</summary>
    public AsyncRelayCommand OpenNutritionCommand { get; }
    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Loads the dashboard.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>The first-load indicator, so a refresh does not blank a dashboard already on screen.</summary>
    public bool IsPageLoading => _loadGate.IsLoading(IsBusy);

    /// <summary>
    /// The classified copy for the last failed load, or null before a failure. The shared overlay
    /// renders it instead of its generic line, so a rejected session, a rate limit and a server fault
    /// no longer all read as one connection problem.
    /// </summary>
    public string? LoadErrorMessage => _lastFailure?.MessageText;

    /// <summary>The retry the last failure permits, or null when repeating it cannot succeed.</summary>
    public AsyncRelayCommand? RetryLoadCommand =>
        _lastFailure is null || _lastFailure.IsRetryable ? LoadDataCommand : null;

    /// <summary>Opens the photo vault, and does nothing when the page was given no way to.</summary>
    private Task OpenPhotoVaultAsync()
    {
        return _openPhotoVaultAsync?.Invoke() ?? Task.CompletedTask;
    }

    /// <summary>Refreshes the nutrition figures when the diary changes.</summary>
    private void OnNutritionChanged(object? sender, NutritionChangedEventArgs e)
    {
        _loadGate.InvalidateAndReloadIfToday(e.Date, LoadDataCoreAsync, this);
    }

    /// <summary>Marks the dashboard stale so the next appearance reloads it after a change made elsewhere.</summary>
    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    /// <summary>Reloads after the profile changed, because the greeting and the targets both come from it.</summary>
    public Task OnProfileChangedAsync()
    {
        Invalidate();
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    /// <summary>Refreshes the profile figures when the profile changes.</summary>
    public void OnProfileChanged()
    {
        _ = OnProfileChangedAsync();
    }

    /// <summary>Reloads after the cycle changed, because the phase card is built from it.</summary>
    public Task OnCycleChangedAsync()
    {
        Invalidate();
        return _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    /// <summary>Refreshes the cycle figures when the cycle changes.</summary>
    public void OnCycleChanged()
    {
        _ = OnCycleChangedAsync();
    }

    /// <summary>Opens the injury modal.</summary>
    [RelayCommand]
    private async Task OpenInjuryModalAsync()
    {
        await InjuryLogVm.OpenModalAsync(() => IsInjuryModalVisible = true);
    }

    /// <summary>Closes the injury modal.</summary>
    [RelayCommand]
    private void CloseInjuryModal()
    {
        _injuryLogVm?.ResetForm();
        IsInjuryModalVisible = false;
    }

    /// <summary>Opens the daily check-in.</summary>
    [RelayCommand]
    private async Task OpenDailyCheckInAsync()
    {
        IsCheckInModalVisible = true;
        await CheckInVm.CheckIfAlreadyCompletedAsync();
    }

    /// <summary>Closes the daily check-in.</summary>
    [RelayCommand]
    private void CloseCheckInModal() => IsCheckInModalVisible = false;

    /// <summary>Drops the check-in draft when the signed-in account changes, so one account's answers can never be saved into</summary>
    /// another's day.</summary>
    internal void ResetCheckInForAccountChange()
    {
        IsCheckInModalVisible = false;
        _checkInVm?.ResetForAccountChange();
        Invalidate();
    }

    /// <summary>
    /// Submitting or skipping closes the sheet and refreshes the dashboard in place. This replaces
    /// the navigation to the dashboard route the check-in page performed when it was a page.
    /// </summary>
    private async Task CloseCheckInAfterCompletionAsync()
    {
        IsCheckInModalVisible = false;
        Invalidate();
        await _loadGate.RunAsync(LoadDataCoreAsync, this, true);
    }

    /// <summary>Refreshes the dashboard after an injury change.</summary>
    private Task RefreshAfterInjuryChangeAsync() =>
        _loadGate.RefreshAfterInjuryChangeAsync(
            InjuryLogVm.HasActiveInjuries,
            hasActiveInjuries => HasActiveInjuries = hasActiveInjuries,
            LoadDataCoreAsync,
            this);

    /// <summary>Repaints the phase card only when the theme really changed, so a redundant call does not rebuild its</summary>
    /// colours.</summary>
    public void RefreshThemeColors(bool useDarkTheme)
    {
        UseDarkTheme = useDarkTheme;
        RefreshPhaseCardColors();
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(ReadinessScore));
        OnPropertyChanged(nameof(RecoveryScore));
    }

    // A units-preference failure keeps the last known system instead of hiding the dashboard.
    /// <summary>Reads the unit preference the trend card formats its figures with.</summary>
    private async Task LoadDisplayUnitsAsync()
    {
        try
        {
            var units = await _profileUnits.LoadAsync();
            if (units.Settings is { } settings)
                _useMetricUnits = settings.UseMetricSystem;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Dashboard] Units preference load failed ({ex.GetType().Name}).");
        }
    }

    /// <summary>Pull-to-refresh, which skips a load already running instead of queueing a second one.</summary>
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

    /// <summary>Opens the workout, and does nothing when the page was given no way to.</summary>
    private Task StartWorkoutAsync()
    {
        return IsRestDay || _todaysWorkoutDayId <= 0
            ? _openWorkoutAsync()
            : _openWorkoutSessionAsync(_todaysWorkoutDayId);
    }

    /// <summary>The dashboard's one load: profile, cycle, plan, nutrition, readiness and trends together, with a failure kept</summary>
    /// for the retry surface instead of leaving half a dashboard on screen.</summary>
    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        IsLoadError = false;
        _lastFailure = null;
        OnPropertyChanged(nameof(LoadErrorMessage));
        OnPropertyChanged(nameof(RetryLoadCommand));
        try
        {
            await LoadDisplayUnitsAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var timeZoneId = TimeZoneInfo.Local.Id;
            var summary = await _overview.LoadAsync(new DashboardOverviewRequest(today, timeZoneId));
            _isBulkLoading = true;
            DisplayName = summary.DisplayName;
            HasActiveInjuries = summary.HasActiveInjuries;
            HasActiveCycle = summary.HasActiveCycle;
            // A baseline prediction is an estimate, not a measured cycle length.
            _isCycleLengthEstimated =
                !string.Equals(summary.PredictionSource, "manual phase log", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(summary.PredictionSource, "profile phase", StringComparison.OrdinalIgnoreCase);
            CurrentPhase = Enum.TryParse<CyclePhase>(summary.CurrentPhase, true, out var phase) && Enum.IsDefined(phase)
                ? phase
                : CyclePhase.Unknown;
            CurrentCycleDay = Math.Max(summary.CurrentCycleDay, 0);
            PredictedCycleLength = Math.Clamp(summary.PredictedCycleLength, 21, 45);
            DaysUntilPeriod = Math.Max(summary.DaysUntilPeriod, 0);
            CycleCount = Math.Max(summary.CycleCount, 0);
            UsePhaseCardColor = summary.HasActiveCycle ||
                                string.Equals(summary.PredictionSource, "manual phase log",
                                    StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(summary.PredictionSource, "profile phase",
                                    StringComparison.OrdinalIgnoreCase);
            ConsumedCalories = summary.ConsumedCalories;
            ConsumedProtein = summary.ConsumedProtein;
            ConsumedCarbs = summary.ConsumedCarbs;
            ConsumedFats = summary.ConsumedFats;
            // Same calorie target as the Nutrition tab: server target plus the on-device Health adjustment.
            TargetCalories = await ApplyHealthCalorieAdjustmentAsync(summary.TargetCalories);
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
            if (summary.TodayReadiness is not null)
            {
                ReadinessScore = summary.ReadinessScore;
                RecoveryScore = summary.RecoveryScore;
                RecoveryLabel = summary.RecoveryLabel;
            }
            else
            {
                ReadinessScore = 0;
                RecoveryScore = 0;
                RecoveryLabel = string.Empty;
            }
            ApplyTodayReadiness(summary.TodayReadiness);
            NeedsDailyCheckIn = summary.TodayReadiness is not { Status: "Available" };
            TrendPoints = (summary.TrendPoints ?? []).Select(point =>
                new DashboardTrendPoint(point.Date, point.WeightKg, point.ReadinessScore)).ToArray();
            HasWeightTrend = TrendPoints.Any(point => point.WeightKg.HasValue);
            HasReadinessTrend = TrendPoints.Any(point => point.ReadinessScore.HasValue);
            var lastWeight = TrendPoints.LastOrDefault(point => point.WeightKg.HasValue)?.WeightKg;
            WeightTrendSummary = lastWeight is null
                ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Weight_TrendEmpty")
                : string.Format(CultureInfo.CurrentCulture,
                    System.Text.CompositeFormat.Parse(
                        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get(
                            "Dashboard_Weight_TrendMedianFormat")),
                    FormatWeight(lastWeight));
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
            IsRestDay = string.Equals(summary.WorkoutActivityTag, "REST", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(summary.WorkoutActivityTag, "Recovery", StringComparison.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(WorkoutCountLabel));
            OnPropertyChanged(nameof(WorkoutCountValue));
            OnPropertyChanged(nameof(WorkoutActionText));
            WorkoutActivitySections = new ObservableCollection<WorkoutActivitySection>(
                (summary.WorkoutActivitySections ?? []).Select(section => new WorkoutActivitySection(
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
            _lastFailure = MuscleCuties.UI.Services.Api.ApiFailurePresenter.FromException(
                ex, "Dashboard_Shared_CouldNotLoadData");
            IsLoadError = true;
            OnPropertyChanged(nameof(LoadErrorMessage));
            OnPropertyChanged(nameof(RetryLoadCommand));
            Trace.WriteLine(
                $"[Dashboard] Load failed ({ex.GetType().Name}) as {_lastFailure.MessageKey}.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Applies today's check-in to the readiness cards, or clears them when there is none.</summary>
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

            if (readiness.SleepQuality is { } sq)
            {
                var checkInRecovery = (sq - MinimumRatingScale) *
                                      MaximumScoreValue / CheckInRecoveryScaleRange;
                RecoveryScore = (int)Math.Round(
                    Math.Clamp(checkInRecovery * CheckInRecoveryWeight + RecoveryScore * BaselineRecoveryWeight,
                        MinimumScoreValue, MaximumScoreValue),
                    MidpointRounding.AwayFromZero);
            }
        }
        else if (!HasTodayReadinessRecord)
        {
            ReadinessScore = 0;
            RecoveryScore = 0;
            RecoveryLabel = string.Empty;
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
    }

    /// <summary>The rating shown for a check-in answer.</summary>
    private static string FormatRating(int? rating) => rating?.ToString(CultureInfo.CurrentCulture) ?? "—";

    /// <summary>Raises the phase properties the page binds.</summary>
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

    /// <summary>Raises the macro properties the page binds.</summary>
    private void NotifyMacroProperties()
    {
        MacroProgressFormatter.NotifyAll(OnPropertyChanged);
        OnPropertyChanged(nameof(NutritionCardAccessibilityText));
    }

    /// <summary>Refreshes the injury banner when injuries change.</summary>
    partial void OnHasActiveInjuriesChanged(bool value)
    {
        OnPropertyChanged(nameof(InjuryButtonAccessibilityText));
    }

    /// <summary>Raises the user's name and greeting properties.</summary>
    private void NotifyUserLinkedProperties()
    {
        OnPropertyChanged(nameof(Greetings));
        OnPropertyChanged(nameof(PhaseShortAdvice));
        // The session state arrives after the badge is first evaluated.
        OnPropertyChanged(nameof(IsWorkoutInProgress));
        OnPropertyChanged(nameof(WorkoutBadgeText));
        OnPropertyChanged(nameof(ShowWorkoutSubtitle));
    }


    /// <summary>How many days remain in the phase she is in.</summary>
    private int CalculateDaysLeftInCurrentPhase()
    {
        return _cyclePhaseProjection.GetDaysRemaining(
            new CyclePhaseCountdownInputDto(CurrentCycleDay, PredictedCycleLength, CurrentPhase)).DaysRemaining;
    }

    /// <summary>The first word of a full name, for the greeting.</summary>
    private static string GetFirstName(string? fullName)
    {
        var trimmed = fullName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        return trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    /// <summary>The first candidate that has content, empty when none of them do.</summary>
    private static string FirstPresent(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    /// <summary>Repaints the phase card from the phase's palette, or from the neutral card colours when the phase colour is
    /// turned off.</summary>
    private void RefreshPhaseCardColors()
    {
        PhaseCardBackgroundColor = UsePhaseCardColor
            ? GetPhaseBackgroundColor(CurrentPhase, UseDarkTheme)
            : PhaseThemePalette.GetNeutralCardBackgroundColor(UseDarkTheme);
        PhaseCardTextColor = UsePhaseCardColor
            ? GetPhaseTextColor(CurrentPhase, UseDarkTheme)
            : PhaseThemePalette.GetNeutralCardTextColor(UseDarkTheme);
        PhaseCardDividerColor = UsePhaseCardColor
            ? PhaseThemePalette.GetPhaseDividerColor(CurrentPhase, UseDarkTheme)
            : PhaseThemePalette.GetNeutralDividerColor(UseDarkTheme);
    }

    /// <summary>The phase card's fill, falling back to the neutral card colour for a phase that has none.</summary>
    private static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseBackgroundColor(phase, useDarkTheme,
            PhaseThemePalette.GetNeutralCardBackgroundColor(useDarkTheme));
    }

    /// <summary>The phase card's ink, with the same neutral fallback so the text stays readable.</summary>
    private static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseTextColor(phase, useDarkTheme,
            PhaseThemePalette.GetNeutralCardTextColor(useDarkTheme));
    }

    /// <summary>Refreshes the phase visuals when the phase changes.</summary>
    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        if (_isBulkLoading) return;
        RefreshPhaseCardColors();
        NotifyPhaseProperties();
    }

    /// <summary>Refreshes the phase card colour when the setting changes.</summary>
    partial void OnUsePhaseCardColorChanged(bool value)
    {
        if (_isBulkLoading) return;
        RefreshPhaseCardColors();
    }

    /// <summary>Rebuilds the themed visuals when the theme changes.</summary>
    partial void OnUseDarkThemeChanged(bool value)
    {
        RefreshPhaseCardColors();
    }

    /// <summary>Refreshes the macro bars when the calories change.</summary>
    partial void OnConsumedCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesConsumed(OnPropertyChanged);
    }

    /// <summary>Refreshes the macro bars when the target changes.</summary>
    partial void OnTargetCaloriesChanged(float value)
    {
        if (_isBulkLoading) return;
        MacroProgressFormatter.NotifyCaloriesGoal(OnPropertyChanged);
    }

    /// <summary>Refreshes the greeting when the name changes.</summary>
    partial void OnDisplayNameChanged(string value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(Greetings));
    }

    /// <summary>Refreshes the cycle summary when the day changes.</summary>
    partial void OnCurrentCycleDayChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
        OnPropertyChanged(nameof(NextPeriodValue));
    }

    /// <summary>Refreshes the cycle summary when the length changes.</summary>
    partial void OnPredictedCycleLengthChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(PhaseStatusText));
        OnPropertyChanged(nameof(PhaseTimeLeftValue));
    }

    /// <summary>Refreshes the countdown when it changes.</summary>
    partial void OnDaysUntilPeriodChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(NextPeriodValue));
    }

    /// <summary>Refreshes the insight card when the text changes.</summary>
    partial void OnCycleInsightTextChanged(string value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(PhaseShortAdvice));
    }

    /// <summary>Refreshes the workout card when the subtitle changes.</summary>
    partial void OnWorkoutSubtitleChanged(string value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(IsWorkoutInProgress));
        OnPropertyChanged(nameof(WorkoutBadgeText));
        OnPropertyChanged(nameof(ShowWorkoutSubtitle));
    }

    /// <summary>Refreshes the workout card when the progress text changes.</summary>
    partial void OnSessionProgressTextChanged(string value)
    {
        OnPropertyChanged(nameof(WorkoutActionText));
        OnPropertyChanged(nameof(IsWorkoutInProgress));
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(WorkoutBadgeText));
        OnPropertyChanged(nameof(IsTodaysWorkoutCompleted));
        OnPropertyChanged(nameof(ShowWorkoutSubtitle));
    }

    /// <summary>Refreshes the streak when the workout days change.</summary>
    partial void OnWorkoutStreakDaysChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(WorkoutStreakText));
    }

    /// <summary>Refreshes the streak when the nutrition days change.</summary>
    partial void OnNutritionStreakDaysChanged(int value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(NutritionStreakText));
    }

    /// <summary>Refreshes the check-in prompt when the need changes.</summary>
    partial void OnNeedsDailyCheckInChanged(bool value)
    {
        OpenDailyCheckInCommand.NotifyCanExecuteChanged();
    }
}
