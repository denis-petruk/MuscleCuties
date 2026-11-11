using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UI.Services.Localization;
using IHealthSyncService = MuscleCuties.Core.Services.Health.IHealthSyncService;
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
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using IPageLoadAware = MuscleCuties.UI.ViewModels.Common.IPageLoadAware;

namespace MuscleCuties.UI.ViewModels.Dashboard;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global",
    Justification = "Bound in XAML / view model contract.")]
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
    [ObservableProperty] private string _readinessLabel = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadinessProgress), nameof(CompositeScore))]
    private int _readinessScore;
    [ObservableProperty] private string _readinessTrendSummary =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Readiness_TrendEmpty");
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

    public AsyncRelayCommand OpenPhotoVaultCommand { get; }

    public bool IsAnyModalVisible => IsInjuryModalVisible || IsCheckInModalVisible;

    public DailyCheckInViewModel CheckInVm => _checkInVm ??= new DailyCheckInViewModel(
        _dailyCheckIn, _invalidateDashboard, CloseCheckInAfterCompletionAsync, _healthSync, _userIdProvider);

    public InjuryLogViewModel InjuryLogVm => _injuryLogVm ??= new InjuryLogViewModel(
        _injuries, () => Task.CompletedTask, RefreshAfterInjuryChangeAsync);

    public string DashboardPhaseHeaderText => CurrentPhase == CyclePhase.Unknown
        ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Phase_MissingHeader")
        : MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
            "Dashboard_Phase_HeaderFormat", LocalizedCurrentPhaseName);

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

    public string WorkoutCountLabel => IsRestDay
        ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_CountActivity")
        : WorkoutExercisesCount.Contains("sets", StringComparison.OrdinalIgnoreCase)
            ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_CountSets")
            : MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Dashboard_Workout_CountExercises");

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

    public string PhaseCardTitle => MuscleCuties.UI.Services.Localization.UiTextCatalog.Get(CurrentPhase switch
    {
        CyclePhase.Menstrual => "Dashboard_Phase_TitleRecovery",
        CyclePhase.Follicular => "Dashboard_Phase_TitleBuild",
        CyclePhase.Ovulatory => "Dashboard_Phase_TitlePower",
        CyclePhase.Luteal => "Dashboard_Phase_TitleSteady",
        _ => "Dashboard_Phase_TitleCycle"
    });

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

    private string LocalizedCurrentPhaseName =>
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get($"Dashboard_Phase_{CurrentPhase}");

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
        : IsWorkoutInProgress
            ? "Today · In Progress"
            : $"Today · {SessionProgressText}";

    // The store reports "Completed (<volume> kg volume)" for a finished session, so match the prefix.
    public bool IsTodaysWorkoutCompleted =>
        SessionProgressText.StartsWith("Completed", StringComparison.OrdinalIgnoreCase);

    // The store reports "Active Session" for a session that has started but not finished.
    public bool IsWorkoutInProgress =>
        string.Equals(SessionProgressText, "Active Session", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(SessionProgressText, "In Progress", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(WorkoutSubtitle, "In Progress", StringComparison.OrdinalIgnoreCase);

    // One label for the Today primary action, so a finished or running session is never offered as "Start".
    public string WorkoutActionText => IsRestDay
        ? UiTextCatalog.Get("Dashboard_Workout_ViewRestDay")
        : IsTodaysWorkoutCompleted
            ? UiTextCatalog.Get("Dashboard_Workout_ViewCompleted")
            : IsWorkoutInProgress
                ? UiTextCatalog.Get("Workout_Overview_ResumeWorkout")
                : UiTextCatalog.Get("Dashboard_Workout_StartWorkout");

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
        : "Dashboard_Readiness_CheckInPrompt";

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

    private readonly IProfilePersonalInfoUseCase? _personalInfo;

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
    public AsyncRelayCommand OpenNutritionCommand { get; }
    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public AsyncRelayCommand LoadDataCommand { get; }
    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    /// <summary>
    /// The classified copy for the last failed load, or null before a failure. The shared overlay
    /// renders it instead of its generic line, so a rejected session, a rate limit and a server fault
    /// no longer all read as one connection problem.
    /// </summary>
    public string? LoadErrorMessage => _lastFailure?.MessageText;

    /// <summary>The retry the last failure permits, or null when repeating it cannot succeed.</summary>
    public AsyncRelayCommand? RetryLoadCommand =>
        _lastFailure is null || _lastFailure.IsRetryable ? LoadDataCommand : null;

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

    [RelayCommand]
    private async Task OpenDailyCheckInAsync()
    {
        IsCheckInModalVisible = true;
        await CheckInVm.CheckIfAlreadyCompletedAsync();
    }

    [RelayCommand]
    private void CloseCheckInModal() => IsCheckInModalVisible = false;

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

    // A units-preference failure keeps the last known system instead of hiding the dashboard.
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
                ReadinessLabel = summary.ReadinessLabel;
                RecoveryScore = summary.RecoveryScore;
                RecoveryLabel = summary.RecoveryLabel;
            }
            else
            {
                ReadinessScore = 0;
                ReadinessLabel = string.Empty;
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
        else if (!HasTodayReadinessRecord)
        {
            ReadinessScore = 0;
            RecoveryScore = 0;
            ReadinessLabel = string.Empty;
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
        // The session state arrives after the badge is first evaluated.
        OnPropertyChanged(nameof(IsWorkoutInProgress));
        OnPropertyChanged(nameof(WorkoutBadgeText));
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

    partial void OnWorkoutSubtitleChanged(string value)
    {
        if (_isBulkLoading) return;
        OnPropertyChanged(nameof(IsWorkoutInProgress));
        OnPropertyChanged(nameof(WorkoutBadgeText));
    }

    partial void OnSessionProgressTextChanged(string value)
    {
        OnPropertyChanged(nameof(WorkoutActionText));
        OnPropertyChanged(nameof(IsWorkoutInProgress));
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
