using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Cycle;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using System.Diagnostics.CodeAnalysis;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using MuscleCuties.UseCases.Features.Dashboard.Overview;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Cycle;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global",
    Justification = "Bound in XAML / view model contract.")]
public partial class CycleViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly Func<DateTime> _currentDateProvider;
    private readonly List<CycleHistoryItem> _cycleHistory = new();
    private readonly ICycleDailyLogUseCase _dailyLog;
    private readonly ICyclePhaseProjectionUseCase _phaseProjection;
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);

    private readonly Func<Task>? _openCycleSettingsAsync;
    private readonly Func<CyclePhase, Task> _openPhaseDetailsAsync;
    private readonly ICycleOverviewUseCase _overview;
    private readonly ICyclePhaseEditUseCase _phaseEdit;
    private readonly IDashboardOverviewUseCase _dashboardOverview;
    private readonly List<CyclePhaseEntry> _phaseLogs = new();
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly List<CycleSymptomEntry> _symptomLogs = new();
    private CyclePredictionSummary? _cachedPrediction;
    [ObservableProperty] private ObservableCollection<CycleDayItem> _calendarDays = new();
    [ObservableProperty] private string _calendarEditHintText = string.Empty;
    [ObservableProperty] private int _currentDay;

    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;
    [ObservableProperty] private int _cycleLength;
    [ObservableProperty] private string _cycleWarningSuggestedActionText = "Use next phase";
    private CyclePhase? _cycleWarningSuggestedPhase;
    [ObservableProperty] private string _cycleWarningText = string.Empty;
    [ObservableProperty] private string _cycleWarningTitle = string.Empty;
    [ObservableProperty] private int _daysUntilPeriod;
    [ObservableProperty] private bool _hasActiveCycle;
    [ObservableProperty] private bool _hasPhaseJumpWarning;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isCycleWarningPopupVisible;
    [ObservableProperty] private bool _isDatePhaseModalVisible;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private string _lastPhaseLogText = "No shift logged yet";
    [ObservableProperty] private ObservableCollection<CyclePhaseOptionItem> _phaseEditOptions = new();
    [ObservableProperty] private string _phaseEditStatusText = string.Empty;
    [ObservableProperty] private string _phaseJumpWarningText = string.Empty;
    [ObservableProperty] private string _phaseJumpWarningTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<PhaseItem> _phases = new();
    [ObservableProperty] private string _selectedCalendarDateText = string.Empty;

    private CycleDayItem? _selectedCalendarDay;
    [ObservableProperty] private string _selectedCalendarPhaseText = string.Empty;
    private CyclePhase? _suggestedPhase;
    [ObservableProperty] private bool _useDarkTheme;

    public CycleViewModel(
        ICycleOverviewUseCase overview,
        ICyclePhaseProjectionUseCase phaseProjection,
        ICycleDailyLogUseCase dailyLog,
        ICyclePhaseEditUseCase phaseEdit,
        IProfileRefreshNotifier refreshNotifier,
        IDashboardOverviewUseCase dashboardOverview,
        Func<CyclePhase, Task>? openPhaseDetailsAsync = null,
        Func<DateTime>? currentDateProvider = null,
        Func<Task>? openCycleSettingsAsync = null)
    {
        _overview = overview;
        _phaseProjection = phaseProjection;
        _dailyLog = dailyLog;
        _phaseEdit = phaseEdit;
        _dashboardOverview = dashboardOverview;
        _refreshNotifier = refreshNotifier;
        _openPhaseDetailsAsync = openPhaseDetailsAsync ?? (_ => Task.CompletedTask);
        _currentDateProvider = currentDateProvider ?? (() => DateTime.Today);
        SelectedDate = CurrentDate;
        DisplayedMonth = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
        _openCycleSettingsAsync = openCycleSettingsAsync;
        OpenCycleSettingsCommand = new AsyncRelayCommand(OpenCycleSettingsAsync);
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync, this));
        AdvancePhaseCommand = new AsyncRelayCommand(AdvancePhaseAsync, CanAdvancePhase);
        OpenCalendarDayCommand = new RelayCommand<CycleDayItem>(OpenCalendarDay);
        CloseDatePhaseModalCommand = new RelayCommand(CloseDatePhaseModal);
        SelectPhaseOptionCommand = new RelayCommand<CyclePhaseOptionItem>(SelectPhaseOption);
        SaveDatePhaseCommand = new AsyncRelayCommand(SaveDatePhaseAsync, CanSaveDatePhase);
        UseSuggestedPhaseCommand = new RelayCommand(UseSuggestedPhase);
        ReviewEarlierPhaseRecordsCommand = new RelayCommand(ReviewEarlierPhaseRecords);
        CloseCycleWarningPopupCommand = new RelayCommand(CloseCycleWarningPopup);
        UseCycleWarningSuggestedPhaseCommand = new AsyncRelayCommand(UseCycleWarningSuggestedPhaseAsync);
        OpenPhaseDetailsCommand = new AsyncRelayCommand<PhaseItem>(OpenPhaseDetailsAsync);
        Phases = BuildPhaseItems(UseDarkTheme);
        InitializeCalendarCommands();
        InitializeQuickLogCommands();
        InitializeGuidanceCommands();
    }

    public string PhaseLabel => CurrentPhase.ToString();
    public string CurrentMonthLabel => DisplayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    public string CycleDaySummary => HasActiveCycle
        ? _cachedPrediction?.PredictionSource == "default"
            ? $"Day {CurrentDay} of an estimated {CycleLength}-day cycle"
            : $"Day {CurrentDay} of {CycleLength}"
        : "No period dates logged";

    public string PeriodCountdownText => HasActiveCycle
        ? DaysUntilPeriod == 0 ? "Period due today" : $"Period in {DaysUntilPeriod}d"
        : "Log period start";

    public string PhaseLogActionText => HasActiveCycle ? "Next phase" : "Log menstrual phase";

    public string PredictionSummary => HasActiveCycle
        ? _cachedPrediction?.PredictionSource == "default"
            ? $"Estimate using a {CycleLength}-day baseline; personal cycle length unavailable"
            : $"Prediction from {CycleLength}d cycle"
        : "No active cycle yet";

    public string CurrentPhaseIconSource => CyclePhaseAssets.GetIconSource(CurrentPhase);
    public string CurrentPhaseVisualSource => CyclePhaseAssets.GetVisualSource(CurrentPhase);
    public bool CurrentPhaseUsesAnimatedVisual => CyclePhaseAssets.UsesAnimatedVisual(CurrentPhase);
    public DateTime Today => CurrentDate;
    public bool HasCalendarEditHint => !string.IsNullOrWhiteSpace(CalendarEditHintText);
    public bool HasPhaseEditStatus => !string.IsNullOrWhiteSpace(PhaseEditStatusText);
    public bool HasSuggestedPhase => _suggestedPhase is not null;
    public bool HasCycleWarningSuggestedPhase => _cycleWarningSuggestedPhase is not null;
    public IReadOnlyList<CyclePhase> PhaseOptions { get; } = Enum.GetValues<CyclePhase>();

    public int CycleDay => CurrentDay;
    public AsyncRelayCommand AdvancePhaseCommand { get; }
    public RelayCommand<CycleDayItem> OpenCalendarDayCommand { get; }
    public RelayCommand CloseDatePhaseModalCommand { get; }
    public RelayCommand<CyclePhaseOptionItem> SelectPhaseOptionCommand { get; }
    public AsyncRelayCommand SaveDatePhaseCommand { get; }
    public RelayCommand UseSuggestedPhaseCommand { get; }
    public RelayCommand ReviewEarlierPhaseRecordsCommand { get; }
    public RelayCommand CloseCycleWarningPopupCommand { get; }
    public AsyncRelayCommand UseCycleWarningSuggestedPhaseCommand { get; }
    public AsyncRelayCommand<PhaseItem> OpenPhaseDetailsCommand { get; }
    public AsyncRelayCommand OpenCycleSettingsCommand { get; }

    private DateTime CurrentDate => _currentDateProvider().Date;

    public AsyncRelayCommand LoadDataCommand { get; }

    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    private async Task OpenCycleSettingsAsync()
    {
        if (_openCycleSettingsAsync is not null) await _openCycleSettingsAsync();
    }

    public void RefreshThemeColors(bool useDarkTheme)
    {
        if (UseDarkTheme == useDarkTheme)
            return;

        UseDarkTheme = useDarkTheme;
    }

    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        try
        {
            await RefreshCycleDataAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshCycleDataAsync()
    {
        var loadVersion = Interlocked.Increment(ref _calendarLoadVersion);
        var displayedMonth = DisplayedMonth;
        var today = CurrentDate;
        var firstDayOfMonth = new DateTime(displayedMonth.Year, displayedMonth.Month, 1);
        var lastDayOfMonth = new DateTime(displayedMonth.Year, displayedMonth.Month,
            DateTime.DaysInMonth(displayedMonth.Year, displayedMonth.Month));

        var selectedAnchor = SelectedDate;
        var dayOfWeek = (int)selectedAnchor.DayOfWeek;
        var diffToMonday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        var monday = selectedAnchor.Date.AddDays(diffToMonday);
        var sunday = monday.AddDays(6);

        var rangeStart = firstDayOfMonth < monday ? firstDayOfMonth : monday;
        var rangeEnd = lastDayOfMonth > sunday ? lastDayOfMonth : sunday;

        var overview = await _overview.LoadAsync(rangeStart, rangeEnd);
        if (loadVersion != Volatile.Read(ref _calendarLoadVersion))
            return;
        if (overview is null)
        {
            IsLoadError = true;
            HasActiveCycle = false;
            CurrentPhase = CyclePhase.Unknown;
            CurrentDay = 0;
            CycleLength = 0;
            DaysUntilPeriod = 0;
            _cachedPrediction = null;
            CalendarDays.Clear();
            _phaseLogs.Clear();
            _cycleHistory.Clear();
            _symptomLogs.Clear();
            return;
        }

        IsLoadError = false;

        var prediction = overview.Prediction;
        _cachedPrediction = prediction;
        OnPropertyChanged(nameof(CycleDaySummary));
        OnPropertyChanged(nameof(PredictionSummary));

        var resolvedPhase = await ResolveCurrentPhaseAsync(prediction.CurrentPhase, today);
        if (loadVersion != Volatile.Read(ref _calendarLoadVersion))
            return;

        HasActiveCycle = prediction.HasActiveCycle;
        CurrentDay = prediction.CurrentDay;
        CycleLength = prediction.PredictedCycleLength;
        DaysUntilPeriod = prediction.DaysUntilPeriod;
        CurrentPhase = resolvedPhase;

        var phaseLogs = overview.PhaseLogs;
        var symptomLogs = overview.Symptoms;
        var cycleHistory = overview.History;

        _phaseLogs.Clear();
        _phaseLogs.AddRange(phaseLogs);
        _cycleHistory.Clear();
        _cycleHistory.AddRange(cycleHistory);
        _symptomLogs.Clear();
        _symptomLogs.AddRange(symptomLogs);

        var latestPhaseLog = phaseLogs.LastOrDefault(log => log.LoggedAt.Date <= today);
        CalendarDays = BuildCalendarDays(prediction, phaseLogs, cycleHistory, displayedMonth, today, SelectedDate,
            UseDarkTheme, symptomLogs);
        RefreshCalendarStrip(CalendarDays, SelectedDate, today, UseDarkTheme);
        RefreshGuidanceContent(CurrentPhase, UseDarkTheme);
        SyncCalendarSelection();
        LastPhaseLogText = FormatLastPhaseLog(latestPhaseLog);
        Phases = BuildPhaseItems(UseDarkTheme);

        OnPropertyChanged(nameof(Today));
        OnPropertyChanged(nameof(PhaseLabel));
        NotifyCycleSummaryProperties();
    }

    private async Task<CyclePhase> ResolveCurrentPhaseAsync(CyclePhase predictionPhase, DateTime today)
    {
        if (predictionPhase != CyclePhase.Unknown)
            return predictionPhase;

        try
        {
            var dashboard = await _dashboardOverview.LoadAsync(
                new DashboardOverviewRequest(DateOnly.FromDateTime(today)));
            if (Enum.TryParse<CyclePhase>(dashboard.CurrentPhase, true, out var selectedPhase) &&
                Enum.IsDefined(selectedPhase))
                return selectedPhase;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Phase fallback load failed ({ex.GetType().Name}).");
        }

        return predictionPhase;
    }

    private void NotifyCycleMutated()
    {
        try
        {
            _refreshNotifier.InvalidateDashboard();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] InvalidateDashboard failed ({ex.GetType().Name}).");
        }
    }

    private async Task AdvancePhaseAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var nextPhase = HasActiveCycle
                ? _phaseProjection.GetNextPhase(new NextCyclePhaseInputDto(CurrentPhase)).NextPhase
                : CyclePhase.Menstrual;
            var date = CurrentDate;
            if (!await _phaseEdit.SaveAsync(new CyclePhaseEditRequest(date, nextPhase,
                    "Manual phase advance")))
                throw new InvalidOperationException("Active cycle account unavailable.");
            await RefreshCycleDataAsync();
            NotifyCycleMutated();
        }
        catch (CyclePhaseOrderIssue ex)
        {
            ShowCycleOrderPopup("This phase would skip a step. Record the missing shift first.", ex.SuggestedPhase);
        }
        catch (InvalidOperationException)
        {
            ShowCycleWarningPopup("Could not change phase yet", "Check the selected date and earlier cycle records.");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Phase advance failed ({ex.GetType().Name}).");
            ShowCycleWarningPopup("Could not save phase", "Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenCalendarDay(CycleDayItem? day)
    {
        day ??= CalendarDays.FirstOrDefault(d => d.Date?.Date == SelectedDate.Date)
                ?? CalendarStripDays.FirstOrDefault(d => d.Date?.Date == SelectedDate.Date)
                ?? new CycleDayItem { Date = SelectedDate, Day = SelectedDate.Day, Phase = CurrentPhase };

        if (day?.Date is null)
            return;

        _selectedCalendarDay = day;
        _suggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        HasPhaseJumpWarning = false;
        PhaseEditStatusText = string.Empty;
        CalendarEditHintText = string.Empty;
        SelectedCalendarDateText = day.Date.Value.ToString("D", CultureInfo.CurrentCulture);
        SelectedCalendarPhaseText = day.IsNeutral
            ? "Neutral, ready when you are"
            : day.HasRecordedObservation
                ? $"{day.Phase} phase (recorded)"
                : day.IsPredictedFuture
                    ? $"{day.Phase} phase (predicted)"
                    : $"{day.Phase} phase (estimated)";
        PhaseEditOptions = BuildPhaseOptions(day.IsNeutral ? null : day.Phase, UseDarkTheme);
        IsDatePhaseModalVisible = true;
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    private async Task OpenPhaseDetailsAsync(PhaseItem? phase)
    {
        if (phase is null)
            return;

        await _openPhaseDetailsAsync(phase.Phase);
    }

    private void CloseDatePhaseModal()
    {
        IsDatePhaseModalVisible = false;
    }

    private void SelectPhaseOption(CyclePhaseOptionItem? option)
    {
        if (option is null)
            return;

        foreach (var phaseOption in PhaseEditOptions)
            phaseOption.IsSelected = false;

        option.IsSelected = true;
        _suggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        HasPhaseJumpWarning = false;
        PhaseEditStatusText = string.Empty;
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    private async Task SaveDatePhaseAsync()
    {
        if (_selectedCalendarDay?.Date is not DateTime date)
            return;

        var selectedPhase = GetSelectedPhase();
        if (selectedPhase is null)
        {
            PhaseEditStatusText = "Pick the phase that fits this day.";
            return;
        }

        await SavePhaseForDateAsync(date, selectedPhase.Value, "Calendar phase edit");
    }

    private void UseSuggestedPhase()
    {
        if (_suggestedPhase is null)
            return;

        foreach (var phaseOption in PhaseEditOptions)
            phaseOption.IsSelected = phaseOption.Phase == _suggestedPhase.Value;

        HasPhaseJumpWarning = false;
        PhaseEditStatusText =
            $"{FormatPhaseName(_suggestedPhase.Value)} keeps the cycle in order. Tap Save phase to log it.";
        _suggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    private void ReviewEarlierPhaseRecords()
    {
        HasPhaseJumpWarning = false;
        IsDatePhaseModalVisible = false;
        IsCycleWarningPopupVisible = false;
        CalendarEditHintText = "Easy fix. Tap the day the missed shift really began.";
        _selectedCalendarDay = null;
        _suggestedPhase = null;
        _cycleWarningSuggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveDatePhase()
    {
        return _selectedCalendarDay?.Date is not null;
    }

    private async Task SavePhaseForDateAsync(DateTime date, CyclePhase phase, string note)
    {
        IsBusy = true;
        try
        {
            if (!await _phaseEdit.SaveAsync(new CyclePhaseEditRequest(date, phase, note)))
                throw new InvalidOperationException("Active cycle account unavailable.");
            CloseDatePhaseModal();
            await RefreshCycleDataAsync();
            NotifyCycleMutated();
        }
        catch (CyclePhaseOrderIssue ex)
        {
            ShowPhaseOrderWarning("This phase would skip a step. Record the missing shift first.", ex.SuggestedPhase);
        }
        catch (InvalidOperationException)
        {
            PhaseJumpWarningTitle = "Let’s keep the cycle in order";
            PhaseJumpWarningText = "Check the selected date and earlier cycle records.";
            HasPhaseJumpWarning = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Phase date save failed ({ex.GetType().Name}).");
            PhaseEditStatusText = "Could not save this phase. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private CyclePhase? GetSelectedPhase()
    {
        return PhaseEditOptions.FirstOrDefault(option => option.IsSelected)?.Phase;
    }

    private bool CanAdvancePhase()
    {
        return !IsBusy;
    }

    private static string FormatLastPhaseLog(CyclePhaseEntry? log)
    {
        return log is null ? "No shift logged yet" : $"{log.Phase} logged {log.LoggedAt:MMM d}";
    }

    private ObservableCollection<CycleDayItem> BuildCalendarDays(
        CyclePredictionSummary prediction,
        IReadOnlyCollection<CyclePhaseEntry> phaseLogs,
        IReadOnlyCollection<CycleHistoryItem> cycleHistory,
        DateTime displayedMonth,
        DateTime currentDate,
        DateTime selectedDate,
        bool useDarkTheme,
        IReadOnlyCollection<CycleSymptomEntry>? symptomLogs = null)
    {
        var items = new ObservableCollection<CycleDayItem>();
        var today = currentDate.Date;
        var firstDayOfMonth = new DateTime(displayedMonth.Year, displayedMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(displayedMonth.Year, displayedMonth.Month);
        var orderedPhaseLogs = phaseLogs
            .OrderBy(log => log.LoggedAt)
            .ThenBy(log => log.CreatedAt)
            .ToList();
        var orderedCycles = cycleHistory.OrderBy(cycle => cycle.StartDate).ToList();

        for (var dayOfMonth = 1; dayOfMonth <= daysInMonth; dayOfMonth++)
        {
            var date = firstDayOfMonth.AddDays(dayOfMonth - 1);
            var item = CreateCalendarDayItem(
                date,
                prediction,
                orderedPhaseLogs,
                orderedCycles,
                symptomLogs,
                today,
                selectedDate,
                useDarkTheme);
            items.Add(item);
        }

        return items;
    }

    private CycleDayItem CreateCalendarDayItem(
        DateTime date,
        CyclePredictionSummary prediction,
        IReadOnlyCollection<CyclePhaseEntry> orderedPhaseLogs,
        IReadOnlyCollection<CycleHistoryItem> orderedCycles,
        IReadOnlyCollection<CycleSymptomEntry>? symptomLogs,
        DateTime today,
        DateTime selectedDate,
        bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.CreateCalendarDayItem(
            date,
            prediction,
            orderedPhaseLogs,
            orderedCycles,
            symptomLogs,
            today,
            selectedDate,
            useDarkTheme,
            _phaseProjection);
    }

    private static Color ResolveDayStrokeColor(
        CyclePhase phase,
        bool isToday,
        bool hasRecordedObservation,
        bool isEstimated,
        bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.ResolveDayStrokeColor(phase, isToday, hasRecordedObservation, isEstimated,
            useDarkTheme);
    }

    private static Color GetDayBackground(CyclePhase phase, bool isToday, bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.GetDayBackground(phase, isToday, useDarkTheme);
    }

    private static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.GetPhaseBackgroundColor(phase, useDarkTheme);
    }

    private static Color GetNeutralDayBackground(bool isToday, bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.GetNeutralDayBackground(isToday, useDarkTheme);
    }

    private static Color GetNeutralDayTextColor(bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.GetNeutralDayTextColor(useDarkTheme);
    }

    private static Color GetTodayDayTextColor(bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.GetTodayDayTextColor(useDarkTheme);
    }

    private static Color GetPredictedDayStrokeColor(CyclePhase phase, bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.GetPredictedDayStrokeColor(phase, useDarkTheme);
    }

    private static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme)
    {
        return CycleCalendarItemBuilder.GetPhaseTextColor(phase, useDarkTheme);
    }

    private static ObservableCollection<PhaseItem> BuildPhaseItems(bool useDarkTheme)
    {
        return new ObservableCollection<PhaseItem>
        {
            new()
            {
                Phase = CyclePhase.Menstrual,
                Name = "Menstrual",
                Description = "Days 1-5 · Low intensity, rest and recover",
                IllustrationSource = CyclePhaseAssets.GetVisualSource(CyclePhase.Menstrual),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Menstrual),
                UsesAnimatedIllustration = CyclePhaseAssets.UsesAnimatedVisual(CyclePhase.Menstrual),
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme)
            },
            new()
            {
                Phase = CyclePhase.Follicular,
                Name = "Follicular",
                Description = "Days 6-13 · Energy rising, great for strength training",
                IllustrationSource = CyclePhaseAssets.GetVisualSource(CyclePhase.Follicular),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Follicular),
                UsesAnimatedIllustration = CyclePhaseAssets.UsesAnimatedVisual(CyclePhase.Follicular),
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme)
            },
            new()
            {
                Phase = CyclePhase.Ovulatory,
                Name = "Ovulatory",
                Description = "Days 14-16 · Peak performance, max intensity",
                IllustrationSource = CyclePhaseAssets.GetVisualSource(CyclePhase.Ovulatory),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Ovulatory),
                UsesAnimatedIllustration = CyclePhaseAssets.UsesAnimatedVisual(CyclePhase.Ovulatory),
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme)
            },
            new()
            {
                Phase = CyclePhase.Luteal,
                Name = "Luteal",
                Description = "Days 17-28 · Moderate exercise, listen to your body",
                IllustrationSource = CyclePhaseAssets.GetVisualSource(CyclePhase.Luteal),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Luteal),
                UsesAnimatedIllustration = CyclePhaseAssets.UsesAnimatedVisual(CyclePhase.Luteal),
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Luteal, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme)
            }
        };
    }

    private static ObservableCollection<CyclePhaseOptionItem> BuildPhaseOptions(CyclePhase? selectedPhase,
        bool useDarkTheme)
    {
        return new ObservableCollection<CyclePhaseOptionItem>(BuildPhaseItems(useDarkTheme).Select(item =>
        {
            return new CyclePhaseOptionItem
            {
                Phase = item.Phase,
                Name = item.Name,
                Description = item.Description,
                IconSource = item.IllustrationSource,
                UsesAnimatedIcon = item.UsesAnimatedIllustration,
                BackgroundColor = item.BackgroundColor,
                TextColor = item.TextColor,
                IsSelected = selectedPhase == item.Phase
            };
        }));
    }

    private void ShowPhaseOrderWarning(string message, CyclePhase suggestedPhase)
    {
        _suggestedPhase = suggestedPhase;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        PhaseJumpWarningTitle = "Keep your rhythm clean";
        PhaseJumpWarningText = FormatCycleOrderWarningMessage(message);
        HasPhaseJumpWarning = true;
    }

    private void ShowCycleOrderPopup(string message, CyclePhase suggestedPhase)
    {
        _cycleWarningSuggestedPhase = suggestedPhase;
        CycleWarningSuggestedActionText = $"Use {suggestedPhase}";
        OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
        ShowCycleWarningPopup("Quick cycle check", FormatCycleOrderWarningMessage(message));
    }

    private void ShowCycleWarningPopup(string title, string message)
    {
        CycleWarningTitle = title;
        CycleWarningText = message;
        IsCycleWarningPopupVisible = true;
    }

    private void ShowCycleRepairPopup(string message)
    {
        _cycleWarningSuggestedPhase = null;
        CycleWarningSuggestedActionText = "Use next phase";
        OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
        ShowCycleWarningPopup(
            "Fix the missed shift first",
            $"{FormatCycleOrderWarningMessage(message)} Tap \"Forgot to log shift\" and choose when the phase really changed.");
    }

    private void CloseCycleWarningPopup()
    {
        IsCycleWarningPopupVisible = false;
    }

    private async Task UseCycleWarningSuggestedPhaseAsync()
    {
        if (_cycleWarningSuggestedPhase is null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var suggestedPhase = _cycleWarningSuggestedPhase.Value;
            var date = CurrentDate;
            if (!await _phaseEdit.SaveAsync(new CyclePhaseEditRequest(date, suggestedPhase,
                    "Manual phase correction")))
                throw new InvalidOperationException("Active cycle account unavailable.");
            CloseCycleWarningPopup();
            await RefreshCycleDataAsync();
            NotifyCycleMutated();
        }
        catch (CyclePhaseOrderIssue)
        {
            ShowCycleRepairPopup("This phase would skip a step.");
        }
        catch (InvalidOperationException)
        {
            _cycleWarningSuggestedPhase = null;
            CycleWarningSuggestedActionText = "Use next phase";
            OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
            ShowCycleWarningPopup("Could not save that yet", "Check the selected date and earlier cycle records.");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Suggested phase save failed ({ex.GetType().Name}).");
            ShowCycleWarningPopup("Could not save phase", "Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatCycleOrderWarningMessage(string message)
    {
        return message
            .Replace("This phase would break the cycle order.", "This phase would skip a step.")
            .Replace("Cycle phases must follow order.", "This phase would skip a step.");
    }

    private static string FormatPhaseName(CyclePhase phase)
    {
        return phase.ToString().ToLowerInvariant();
    }

    partial void OnUseDarkThemeChanged(bool value)
    {
        ApplyThemeColors();
    }

    private void ApplyThemeColors()
    {
        Phases = BuildPhaseItems(UseDarkTheme);

        if (PhaseEditOptions.Count > 0)
            PhaseEditOptions = BuildPhaseOptions(GetSelectedPhase(), UseDarkTheme);

        if (CalendarDays.Count == 0)
            return;

        CalendarDays = new ObservableCollection<CycleDayItem>(CalendarDays.Select(day =>
            new CycleDayItem
            {
                Day = day.Day,
                CycleDay = day.CycleDay,
                Date = day.Date,
                Phase = day.Phase,
                IconSource = CyclePhaseAssets.GetIconSource(day.Phase),
                IsToday = day.IsToday,
                IsSelected = day.IsSelected,
                IsNeutral = day.IsNeutral,
                HasPhaseShiftLog = day.HasPhaseShiftLog,
                HasSymptomLog = day.HasSymptomLog,
                IsEstimated = day.IsEstimated,
                IsPredictedFuture = day.IsPredictedFuture,
                BackgroundColor = day.IsNeutral
                    ? GetNeutralDayBackground(day.IsToday, UseDarkTheme)
                    : GetDayBackground(day.Phase, day.IsToday, UseDarkTheme),
                TextColor = day.IsToday
                    ? GetTodayDayTextColor(UseDarkTheme)
                    : day.IsNeutral
                        ? GetNeutralDayTextColor(UseDarkTheme)
                        : GetPhaseTextColor(day.Phase, UseDarkTheme),
                StrokeColor = ResolveDayStrokeColor(
                    day.Phase,
                    day.IsToday,
                    day.HasRecordedObservation,
                    day.IsEstimated,
                    UseDarkTheme),
                StrokeThickness = day.StrokeThickness,
                DayOfWeekShort = day.DayOfWeekShort,
                PhaseMarkerColor = day.IsNeutral
                    ? PhaseThemePalette.GetNeutralDividerColor(UseDarkTheme)
                    : PhaseThemePalette.GetPhaseTextColor(day.Phase, UseDarkTheme, default)
            }));

        RefreshCalendarStrip(CalendarDays, SelectedDate, CurrentDate, UseDarkTheme);
        RefreshGuidanceContent(CurrentPhase, UseDarkTheme);
    }

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(PhaseLogActionText));
        OnPropertyChanged(nameof(CurrentPhaseIconSource));
        OnPropertyChanged(nameof(CurrentPhaseVisualSource));
        OnPropertyChanged(nameof(CurrentPhaseUsesAnimatedVisual));
        RefreshGuidanceContent(value, UseDarkTheme);
    }

    partial void OnCurrentDayChanged(int value)
    {
        NotifyCycleSummaryProperties();
    }

    partial void OnCycleLengthChanged(int value)
    {
        NotifyCycleSummaryProperties();
    }

    partial void OnDaysUntilPeriodChanged(int value)
    {
        OnPropertyChanged(nameof(PeriodCountdownText));
    }

    partial void OnHasActiveCycleChanged(bool value)
    {
        NotifyCycleSummaryProperties();
    }

    partial void OnIsBusyChanged(bool value)
    {
        AdvancePhaseCommand.NotifyCanExecuteChanged();
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    partial void OnCalendarEditHintTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasCalendarEditHint));
    }

    partial void OnPhaseEditStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasPhaseEditStatus));
    }

    private void NotifyCycleSummaryProperties()
    {
        OnPropertyChanged(nameof(CycleDaySummary));
        OnPropertyChanged(nameof(PeriodCountdownText));
        OnPropertyChanged(nameof(PhaseLogActionText));
        OnPropertyChanged(nameof(PredictionSummary));
    }

}
