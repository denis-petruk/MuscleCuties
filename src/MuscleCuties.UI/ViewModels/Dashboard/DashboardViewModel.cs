using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Models.UI.Dashboard;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Workout.Injury;
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
    private DateOnly? _calendarCycleStart;
    private string _calendarPredictionSource = string.Empty;
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
    [ObservableProperty] private Color _phaseCardBackgroundColor = Color.FromArgb("#FFF1F6");
    [ObservableProperty] private Color _phaseCardDividerColor = Color.FromArgb("#EBD3DE");
    [ObservableProperty] private Color _phaseCardTextColor = Color.FromArgb("#8B4E68");
    [ObservableProperty] private string _photoButtonText = "Check in";
    [ObservableProperty] private string _photoCheckInText = "Add today's progress photo";
    [ObservableProperty] private int _predictedCycleLength = 28;
    [ObservableProperty] private string _readinessLabel = string.Empty;
    [ObservableProperty] private int _readinessScore;
    [ObservableProperty] private string _readinessTrendSummary = "Complete a check-in to see readiness history";
    [ObservableProperty] private string _recoveryLabel = string.Empty;
    [ObservableProperty] private int _recoveryScore;
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
        Func<Task> openCycleAsync,
        Func<Task> openWorkoutAsync,
        Func<int, Task> openWorkoutSessionAsync,
        Func<Task> openNutritionAsync,
        Func<Task> openDailyCheckInAsync,
        INutritionChangeNotifier? changeNotifier = null,
        Func<Task>? openPhotoVaultAsync = null)
    {
        _overview = overview;
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
        OpenPhotoVaultCommand = new AsyncRelayCommand(OpenPhotoVaultAsync);
    }

    public AsyncRelayCommand OpenPhotoVaultCommand { get; }

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
            : $"{CurrentPhase} · Start tracking";

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
        CyclePhase.Menstrual => FirstPresent(CycleInsightText, "Take it easy, prioritize rest"),
        CyclePhase.Follicular => FirstPresent(CycleInsightText, "Energy rising, great time to train"),
        CyclePhase.Ovulatory => FirstPresent(CycleInsightText, "Peak energy and strength"),
        CyclePhase.Luteal => FirstPresent(CycleInsightText, "Moderate intensity, listen to your body"),
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

    public int CompositeScore => (ReadinessScore + RecoveryScore) / 2;

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Dashboard XAML / view model contract.")]
    public bool HasApprovedScorePolicy => false;

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in Dashboard XAML / view model contract.")]
    public string ScoreSourceLabel =>
        "Numerical readiness and recovery guidance is unavailable pending qualified review. You can still record a daily check-in.";

    public bool HasAnyTrend => HasWeightTrend || (HasApprovedScorePolicy && HasReadinessTrend);

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
        IsBusy = true;
        IsLoadError = false;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var summary = await _overview.LoadAsync(new DashboardOverviewRequest(today));
            _isBulkLoading = true;
            DisplayName = summary.DisplayName;
            NeedsDailyCheckIn = summary.NeedsDailyCheckIn;
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
            _calendarCycleStart = summary.CurrentCycleStartDate;
            _calendarPredictionSource = summary.PredictionSource;
            _isBulkLoading = false;
            RefreshPhaseCardColors();
            NotifyPhaseProperties();
            NotifyUserLinkedProperties();
            NotifyMacroProperties();
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
        var cycleLength = CyclePhaseRule.NormalizeCycleLength(PredictedCycleLength);
        var currentDay = Math.Clamp(CurrentCycleDay, 1, cycleLength);
        var daysLeft = 0;

        for (var offset = 1; offset <= cycleLength; offset++)
        {
            var projectedDay = (currentDay - 1 + offset) % cycleLength + 1;
            if (CyclePhaseRule.CalculatePhase(projectedDay, cycleLength) != CurrentPhase)
                break;

            daysLeft++;
        }

        return daysLeft;
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
        OnPropertyChanged(nameof(ScoreSourceLabel));
    }
}
