using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Services.Dashboard;
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
/// <summary>The cycle page: where the user is today, the predicted next period and phases, and the month calendar whose</summary>
/// days she edits her logs on.</summary>
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
    [ObservableProperty] private string _cycleWarningSuggestedActionText =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Warning_UseNextPhase");
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
    [ObservableProperty] private bool _isRefreshing;
    private Services.Api.ApiFailureNotice? _lastLoadFailure;
    [ObservableProperty] private string _lastPhaseLogText =
        MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Phase_NoShiftLogged");
    [ObservableProperty] private ObservableCollection<CyclePhaseOptionItem> _phaseEditOptions = new();
    [ObservableProperty] private string _phaseEditStatusText = string.Empty;
    [ObservableProperty] private string _phaseJumpWarningText = string.Empty;
    [ObservableProperty] private string _phaseJumpWarningTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<PhaseItem> _phases = new();
    [ObservableProperty] private string _selectedCalendarDateText = string.Empty;

    private CycleDayItem? _selectedCalendarDay;
    private DateTime? _selectedDateBeforeCalendarEdit;
    [ObservableProperty] private string _selectedCalendarPhaseText = string.Empty;
    private CyclePhase? _suggestedPhase;
    [ObservableProperty] private bool _useDarkTheme;

    /// <summary>Takes the cycle use cases and the navigation callbacks.</summary>
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
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AdvancePhaseCommand = new AsyncRelayCommand(AdvancePhaseAsync, CanAdvancePhase);
        CloseDatePhaseModalCommand = new RelayCommand(CloseDatePhaseModal);
        SelectPhaseOptionCommand = new RelayCommand<CyclePhaseOptionItem>(SelectPhaseOption);
        SaveDatePhaseCommand = new AsyncRelayCommand(SaveDatePhaseAsync, CanSaveDatePhase);
        UseSuggestedPhaseCommand = new RelayCommand(UseSuggestedPhase);
        ReviewEarlierPhaseRecordsCommand = new RelayCommand(ReviewEarlierPhaseRecords);
        CloseCycleWarningPopupCommand = new RelayCommand(CloseCycleWarningPopup);
        UseCycleWarningSuggestedPhaseCommand = new AsyncRelayCommand(UseCycleWarningSuggestedPhaseAsync);
        Phases = BuildPhaseItems(UseDarkTheme);
        InitializeCalendarCommands();
        InitializeQuickLogCommands();
        InitializeGuidanceCommands();
    }

    /// <summary>The current phase by its display name.</summary>
    public string PhaseLabel => CurrentPhase.ToString();
    /// <summary>The month on screen, written out in full.</summary>
    public string CurrentMonthLabel => DisplayFormat.MonthYearFull(DisplayedMonth);

    /// <summary>Which day of the cycle today is, and whether the length behind it is her own or only the fallback</summary>
    /// baseline — the difference matters to her, so the wording says so.</summary>
    public string CycleDaySummary => HasActiveCycle
        ? _cachedPrediction?.PredictionSource == "default"
            ? $"Day {CurrentDay} of an estimated {CycleLength}-day cycle"
            : $"Day {CurrentDay} of {CycleLength}"
        : "No period dates logged";

    /// <summary>Days until the next period, or the prompt to log one when no cycle is running.</summary>
    public string PeriodCountdownText => HasActiveCycle
        ? DaysUntilPeriod == 0 ? "Period due today" : $"Period in {DaysUntilPeriod}d"
        : "Log period start";

    /// <summary>The phase action either advances a running cycle or starts one, so its label follows that state.</summary>
    public string PhaseLogActionText => HasActiveCycle ? "Next phase" : "Log menstrual phase";

    /// <summary>True when the prediction came from the cache.</summary>
    public bool IsPredictionCached => _cachedPrediction?.CachedAsOfDate is not null;

    /// <summary>Where the prediction came from — cached, a default baseline because her own cycle length is unknown, or her</summary>
    /// own length — and says plainly when there is no cycle yet.</summary>
    public string PredictionSummary => HasActiveCycle
        ? IsPredictionCached
            ? MuscleCuties.UI.Services.Localization.UiTextCatalog.Format("Ui_Cycle_CachedEstimateFormat",
                DisplayFormat.ShortDate(_cachedPrediction!.CachedAsOfDate!.Value))
            : _cachedPrediction?.PredictionSource == "default"
                ? $"Estimate using a {CycleLength}-day baseline; personal cycle length unavailable"
                : $"Prediction from {CycleLength}d cycle"
        : "No active cycle yet";

    /// <summary>The icon for the current phase.</summary>
    public string CurrentPhaseIconSource => CyclePhaseAssets.GetIconSource(CurrentPhase);
    /// <summary>The illustration for the current phase.</summary>
    public string CurrentPhaseVisualSource => CyclePhaseAssets.GetVisualSource(CurrentPhase);
    /// <summary>Whether the phase has an animation, which decides whether the animated view is used instead of the still</summary>
    /// one.</summary>
    public bool CurrentPhaseUsesAnimatedVisual => CyclePhaseAssets.UsesAnimatedVisual(CurrentPhase);
    /// <summary>The date the calendar treats as today in the account's time zone.</summary>
    public DateTime Today => CurrentDate;
    /// <summary>True when a calendar edit hint is shown.</summary>
    public bool HasCalendarEditHint => !string.IsNullOrWhiteSpace(CalendarEditHintText);
    /// <summary>True when a phase edit status is shown.</summary>
    public bool HasPhaseEditStatus => !string.IsNullOrWhiteSpace(PhaseEditStatusText);
    /// <summary>True when a phase suggestion is available.</summary>
    public bool HasSuggestedPhase => _suggestedPhase is not null;
    /// <summary>True when the cycle warning suggests a phase.</summary>
    public bool HasCycleWarningSuggestedPhase => _cycleWarningSuggestedPhase is not null;

    /// <summary>The current cycle day.</summary>
    public int CycleDay => CurrentDay;
    /// <summary>Advances to the next phase.</summary>
    public AsyncRelayCommand AdvancePhaseCommand { get; }
    /// <summary>Closes the date phase modal.</summary>
    public RelayCommand CloseDatePhaseModalCommand { get; }
    /// <summary>Selects a phase option.</summary>
    public RelayCommand<CyclePhaseOptionItem> SelectPhaseOptionCommand { get; }
    /// <summary>Saves the phase for the chosen date.</summary>
    public AsyncRelayCommand SaveDatePhaseCommand { get; }
    /// <summary>Applies the suggested phase.</summary>
    public RelayCommand UseSuggestedPhaseCommand { get; }
    /// <summary>Opens the earlier phase records.</summary>
    public RelayCommand ReviewEarlierPhaseRecordsCommand { get; }
    /// <summary>Closes the cycle warning.</summary>
    public RelayCommand CloseCycleWarningPopupCommand { get; }
    /// <summary>Applies the phase the warning suggests.</summary>
    public AsyncRelayCommand UseCycleWarningSuggestedPhaseCommand { get; }
    /// <summary>Opens the cycle settings.</summary>
    public AsyncRelayCommand OpenCycleSettingsCommand { get; }

    private const string PhaseOrderConflictMessage =
        "This phase would skip a step. Record the missing shift first.";
    private const string PhaseEditUnavailableTitleKey = "Cycle_PhaseEdit_UnavailableTitle";
    private const string PhaseEditUnavailableBodyKey = "Cycle_PhaseEdit_UnavailableBody";
    private const string PhaseEditInvalidTitleKey = "Cycle_PhaseEdit_InvalidTitle";
    private const string PhaseEditInvalidBodyKey = "Cycle_PhaseEdit_InvalidBody";

    /// <summary>Maps a non-order outcome to localized copy; returns false when the edit saved.</summary>
    private static bool TryGetPhaseEditFailureCopy(CyclePhaseEditResult result, out string titleKey, out string bodyKey)
    {
        switch (result.Outcome)
        {
            case CyclePhaseEditOutcome.InvalidInput:
                titleKey = PhaseEditInvalidTitleKey;
                bodyKey = PhaseEditInvalidBodyKey;
                return true;
            case CyclePhaseEditOutcome.Unavailable:
                titleKey = PhaseEditUnavailableTitleKey;
                bodyKey = PhaseEditUnavailableBodyKey;
                return true;
            default:
                titleKey = string.Empty;
                bodyKey = string.Empty;
                return false;
        }
    }

    /// <summary>Today from the injected clock, so the page can be exercised on a fixed date.</summary>
    private DateTime CurrentDate => _currentDateProvider().Date;

    /// <summary>Loads the cycle data.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }

    /// <summary>The first-load indicator, so a calendar refresh does not blank the page.</summary>
    public bool IsPageLoading => _loadGate.IsLoading(IsBusy);

    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Marks the page stale so the next appearance reloads after a log was changed elsewhere.</summary>
    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    /// <summary>Opens the cycle settings, when the page was given a way to.</summary>
    private async Task OpenCycleSettingsAsync()
    {
        if (_openCycleSettingsAsync is not null) await _openCycleSettingsAsync();
    }

    /// <summary>Re-reads the phase colours only when the theme really changed, so a redundant call does not repaint the</summary>
    /// whole calendar.</summary>
    public void RefreshThemeColors(bool useDarkTheme)
    {
        if (UseDarkTheme == useDarkTheme)
            return;

        UseDarkTheme = useDarkTheme;
    }

    /// <summary>Reloads the cycle data.</summary>
    public AsyncRelayCommand RefreshCommand { get; }
    /// <summary>The API's own message for the last failed load, with a generic one when there was none.</summary>
    public string LoadErrorMessage => _lastLoadFailure?.MessageText ?? UiTextCatalog.Get("Dashboard_Shared_CouldNotLoadData");
    /// <summary>The retry is offered only for a failure a retry can fix; a refusal that will not change hides it.</summary>
    public System.Windows.Input.ICommand? RetryLoadCommand => _lastLoadFailure?.IsRetryable == false ? null : LoadDataCommand;

    /// <summary>Pull-to-refresh, which skips a load already running instead of queueing a second one.</summary>
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        IsRefreshing = true;
        try
        {
            _loadGate.MarkStale();
            await _loadGate.RunAsync(LoadDataCoreAsync, this);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>The first load: the cycle, its prediction and the month on screen, with failures kept for the retry</summary>
    /// surface.</summary>
    private async Task LoadDataCoreAsync()
    {
        IsBusy = true;
        _lastLoadFailure = null;
        OnPropertyChanged(nameof(LoadErrorMessage));
        OnPropertyChanged(nameof(RetryLoadCommand));
        try
        {
            await RefreshCycleDataAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[CycleViewModel] Load failed ({ex.GetType().Name}).");
            _lastLoadFailure = Services.Api.ApiFailurePresenter.FromException(ex, "Dashboard_Shared_CouldNotLoadData");
            IsLoadError = true;
            OnPropertyChanged(nameof(LoadErrorMessage));
            OnPropertyChanged(nameof(RetryLoadCommand));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Reloads the cycle and rebuilds the calendar under a version, so a slow older answer cannot overwrite a newer</summary>
    /// one.</summary>
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
        OnPropertyChanged(nameof(IsPredictionCached));
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

    /// <summary>The phase to show: the prediction's, unless it could not say, in which case the stored cycle is asked.</summary>
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

    /// <summary>Tells the rest of the app that the cycle changed, tolerating a listener that throws so one broken subscriber</summary>
    /// cannot fail the write that just succeeded.</summary>
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

    /// <summary>Moves the cycle to its next phase and reloads, refusing while another write is running.</summary>
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

            var confirmed = await Shell.Current.DisplayAlertAsync(
                UiTextCatalog.Get("Cycle_Advance_ConfirmTitle"),
                UiTextCatalog.Get("Cycle_Advance_ConfirmMessage"),
                UiTextCatalog.Get("Cycle_Advance_ConfirmAction"),
                UiTextCatalog.Get("Common_Cancel"));
            if (!confirmed)
                return;

            var result = await _phaseEdit.SaveAsync(new CyclePhaseEditRequest(CurrentDate, nextPhase,
                "Manual phase advance"));

            if (result.Outcome == CyclePhaseEditOutcome.OrderConflict && result.SuggestedPhase is { } suggestedPhase)
            {
                ShowCycleOrderPopup(PhaseOrderConflictMessage, suggestedPhase);
                return;
            }

            if (TryGetPhaseEditFailureCopy(result, out var titleKey, out var bodyKey))
            {
                ShowCycleWarningPopup(titleKey, bodyKey);
                return;
            }

            await RefreshCycleDataAsync();
            NotifyCycleMutated();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Phase advance failed ({ex.GetType().Name}).");
            ShowCycleWarningPopup(PhaseEditUnavailableTitleKey, PhaseEditUnavailableBodyKey);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Opens the editor for a tapped day, falling back to the day already selected when the tap carried no item.</summary>
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
        PhaseEditStatusText = day.Date.Value.Date > CurrentDate.Date
            ? UiTextCatalog.Get("Cycle_PhaseEdit_FutureHint")
            : string.Empty;
        CalendarEditHintText = string.Empty;
        SelectedCalendarDateText = DisplayFormat.FullDate(day.Date.Value);
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

    /// <summary>Closes the modal and restores the previous selection.</summary>
    private void CloseDatePhaseModal() => CloseDatePhaseModal(restoreSelection: true);

    /// <summary>Closes the day editor and, when asked, puts the calendar's selection back where it was before the edit.</summary>
    private void CloseDatePhaseModal(bool restoreSelection)
    {
        IsDatePhaseModalVisible = false;
        if (restoreSelection &&
            _selectedDateBeforeCalendarEdit is { } previous &&
            previous.Date != SelectedDate.Date)
        {
            SelectedDate = previous;
            SyncCalendarSelection();
            RefreshCalendarStrip(CalendarDays, SelectedDate, CurrentDate, UseDarkTheme);
        }

        _selectedDateBeforeCalendarEdit = null;
    }

    /// <summary>Records the phase chosen for the day being edited.</summary>
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

    /// <summary>Saves the phase chosen for the day; a refusal keeps the editor open with its reason instead of closing on a</summary>
    /// write that did not happen.</summary>
    private async Task SaveDatePhaseAsync()
    {
        if (_selectedCalendarDay?.Date is not DateTime date)
            return;

        if (date.Date > CurrentDate.Date)
        {
            PhaseEditStatusText = UiTextCatalog.Get("Cycle_PhaseEdit_FutureHint");
            return;
        }

        var selectedPhase = GetSelectedPhase();
        if (selectedPhase is null)
        {
            PhaseEditStatusText = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_PhaseEdit_ChoosePhase");
            return;
        }

        await SavePhaseForDateAsync(date, selectedPhase.Value, "Calendar phase edit");
    }

    /// <summary>Accepts the phase the order warning suggested, instead of the one the user had picked.</summary>
    private void UseSuggestedPhase()
    {
        if (_suggestedPhase is null)
            return;

        foreach (var phaseOption in PhaseEditOptions)
            phaseOption.IsSelected = phaseOption.Phase == _suggestedPhase.Value;

        HasPhaseJumpWarning = false;
        PhaseEditStatusText = MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
            "Cycle_PhaseEdit_SuggestedFormat",
            FormatPhaseName(_suggestedPhase.Value));
        _suggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Shows the earlier records that made this change look out of order, so the user can judge the conflict rather</summary>
    /// than take the warning on trust.</summary>
    private void ReviewEarlierPhaseRecords()
    {
        HasPhaseJumpWarning = false;
        IsDatePhaseModalVisible = false;
        IsCycleWarningPopupVisible = false;
        CalendarEditHintText =
            MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_PhaseEdit_ReviewEarlierHint");
        _selectedCalendarDay = null;
        _suggestedPhase = null;
        _cycleWarningSuggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    /// <summary>An edit can be saved once a phase is chosen and no other write is running.</summary>
    private bool CanSaveDatePhase()
    {
        return _selectedCalendarDay?.Date is DateTime date && date.Date <= CurrentDate.Date;
    }

    /// <summary>The single write for one day's phase and its note, shared by this page's two entry points.</summary>
    private async Task SavePhaseForDateAsync(DateTime date, CyclePhase phase, string note)
    {
        IsBusy = true;
        try
        {
            var result = await _phaseEdit.SaveAsync(new CyclePhaseEditRequest(date, phase, note));

            if (result.Outcome == CyclePhaseEditOutcome.OrderConflict && result.SuggestedPhase is { } suggestedPhase)
            {
                ShowPhaseOrderWarning(PhaseOrderConflictMessage, suggestedPhase);
                return;
            }

            if (TryGetPhaseEditFailureCopy(result, out var titleKey, out var bodyKey))
            {
                PhaseJumpWarningTitle = titleKey;
                PhaseJumpWarningText = bodyKey;
                HasPhaseJumpWarning = true;
                return;
            }

            CloseDatePhaseModal(restoreSelection: false);
            await RefreshCycleDataAsync();
            CalendarEditHintText = UiTextCatalog.Format(
                "Cycle_PhaseEdit_SavedFormat", DisplayFormat.DayMonth(date));
            NotifyCycleMutated();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Phase date save failed ({ex.GetType().Name}).");
            PhaseEditStatusText = PhaseEditUnavailableBodyKey;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The chosen phase option's value, or nothing while no option is selected.</summary>
    private CyclePhase? GetSelectedPhase()
    {
        return PhaseEditOptions.FirstOrDefault(option => option.IsSelected)?.Phase;
    }

    /// <summary>Advancing needs a running cycle and no write in flight.</summary>
    private bool CanAdvancePhase()
    {
        return !IsBusy;
    }

    /// <summary>The last logged phase as one line with when it was logged, or empty when nothing has been logged.</summary>
    private static string FormatLastPhaseLog(CyclePhaseEntry? log)
    {
        return log is null
            ? "No shift logged yet"
            : MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
                "Cycle_Calendar_LastLogFormat",
                DisplayFormat.DayMonth(log.LoggedAt));
    }

    /// <summary>The calendar days for the prediction and logs.</summary>
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

    /// <summary>One calendar day item.</summary>
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

    /// <summary>The phase legend, rebuilt for the theme so its swatches match the calendar.</summary>
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
                BackgroundColor = CycleCalendarItemBuilder.GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme),
                TextColor = CycleCalendarItemBuilder.GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme)
            },
            new()
            {
                Phase = CyclePhase.Follicular,
                Name = "Follicular",
                Description = "Days 6-13 · Energy rising, great for strength training",
                IllustrationSource = CyclePhaseAssets.GetVisualSource(CyclePhase.Follicular),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Follicular),
                UsesAnimatedIllustration = CyclePhaseAssets.UsesAnimatedVisual(CyclePhase.Follicular),
                BackgroundColor = CycleCalendarItemBuilder.GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme),
                TextColor = CycleCalendarItemBuilder.GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme)
            },
            new()
            {
                Phase = CyclePhase.Ovulatory,
                Name = "Ovulatory",
                Description = "Days 14-16 · Peak performance, max intensity",
                IllustrationSource = CyclePhaseAssets.GetVisualSource(CyclePhase.Ovulatory),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Ovulatory),
                UsesAnimatedIllustration = CyclePhaseAssets.UsesAnimatedVisual(CyclePhase.Ovulatory),
                BackgroundColor = CycleCalendarItemBuilder.GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme),
                TextColor = CycleCalendarItemBuilder.GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme)
            },
            new()
            {
                Phase = CyclePhase.Luteal,
                Name = "Luteal",
                Description = "Days 17-28 · Moderate exercise, listen to your body",
                IllustrationSource = CyclePhaseAssets.GetVisualSource(CyclePhase.Luteal),
                IconSource = CyclePhaseAssets.GetIconSource(CyclePhase.Luteal),
                UsesAnimatedIllustration = CyclePhaseAssets.UsesAnimatedVisual(CyclePhase.Luteal),
                BackgroundColor = CycleCalendarItemBuilder.GetPhaseBackgroundColor(CyclePhase.Luteal, useDarkTheme),
                TextColor = CycleCalendarItemBuilder.GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme)
            }
        };
    }

    /// <summary>The phase options with the given one selected.</summary>
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

    /// <summary>Warns that the chosen phase does not follow the last logged one, and offers the phase that would.</summary>
    private void ShowPhaseOrderWarning(string message, CyclePhase suggestedPhase)
    {
        _suggestedPhase = suggestedPhase;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        PhaseJumpWarningTitle = MuscleCuties.UI.Services.Localization.UiTextCatalog.Get(
            "Cycle_Warning_OrderTitle");
        PhaseJumpWarningText = FormatCycleOrderWarningMessage(message);
        HasPhaseJumpWarning = true;
    }

    /// <summary>The same warning for a cycle that would start out of order, carrying the phase it suggests.</summary>
    private void ShowCycleOrderPopup(string message, CyclePhase suggestedPhase)
    {
        _cycleWarningSuggestedPhase = suggestedPhase;
        CycleWarningSuggestedActionText = MuscleCuties.UI.Services.Localization.UiTextCatalog.Format(
            "Cycle_Warning_UsePhaseFormat", FormatPhaseName(suggestedPhase));
        OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
        ShowCycleWarningPopup(
            MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Warning_QuickCheckTitle"),
            FormatCycleOrderWarningMessage(message));
    }

    /// <summary>Shows a warning about the cycle without a suggestion to apply.</summary>
    private void ShowCycleWarningPopup(string title, string message)
    {
        CycleWarningTitle = title;
        CycleWarningText = message;
        IsCycleWarningPopupVisible = true;
    }

    /// <summary>Dismisses the warning and forgets the phase it was suggesting, so a later popup cannot apply a stale one.</summary>
    private void CloseCycleWarningPopup()
    {
        IsCycleWarningPopupVisible = false;
    }

    /// <summary>Applies the warning's suggested phase and closes it.</summary>
    private async Task UseCycleWarningSuggestedPhaseAsync()
    {
        if (_cycleWarningSuggestedPhase is null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var suggestedPhase = _cycleWarningSuggestedPhase.Value;
            var result = await _phaseEdit.SaveAsync(new CyclePhaseEditRequest(CurrentDate, suggestedPhase,
                "Manual phase correction"));

            if (result.Outcome == CyclePhaseEditOutcome.OrderConflict && result.SuggestedPhase is { } nextSuggestion)
            {
                ShowCycleOrderPopup(PhaseOrderConflictMessage, nextSuggestion);
                return;
            }

            if (TryGetPhaseEditFailureCopy(result, out var titleKey, out var bodyKey))
            {
                ShowCycleWarningPopup(titleKey, bodyKey);
                return;
            }

            CloseCycleWarningPopup();
            await RefreshCycleDataAsync();
            NotifyCycleMutated();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Suggested phase save failed ({ex.GetType().Name}).");
            ShowCycleWarningPopup(PhaseEditUnavailableTitleKey, PhaseEditUnavailableBodyKey);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The order warning with the suggested phase named in it.</summary>
    private static string FormatCycleOrderWarningMessage(string message)
    {
        return message
            .Replace("This phase would break the cycle order.", "This phase would skip a step.")
            .Replace("Cycle phases must follow order.", "This phase would skip a step.");
    }

    /// <summary>A phase by the name the app shows for it.</summary>
    private static string FormatPhaseName(CyclePhase phase)
    {
        return MuscleCuties.UI.Services.Localization.UiTextCatalog.Get(
            $"Cycle_Phase_{phase}_Name");
    }

    /// <summary>Rebuilds the themed visuals when the theme changes.</summary>
    partial void OnUseDarkThemeChanged(bool value)
    {
        ApplyThemeColors();
    }

    /// <summary>Re-derives every theme-dependent colour at once after the theme changed, so no cell keeps the old palette.</summary>
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
                    ? CycleCalendarItemBuilder.GetNeutralDayBackground(day.IsToday, UseDarkTheme)
                    : CycleCalendarItemBuilder.GetDayBackground(day.Phase, day.IsToday, UseDarkTheme),
                TextColor = day.IsToday
                    ? CycleCalendarItemBuilder.GetTodayDayTextColor(day.IsNeutral, UseDarkTheme)
                    : day.IsNeutral
                        ? CycleCalendarItemBuilder.GetNeutralDayTextColor(UseDarkTheme)
                        : CycleCalendarItemBuilder.GetPhaseTextColor(day.Phase, UseDarkTheme),
                StrokeColor = CycleCalendarItemBuilder.ResolveDayStrokeColor(
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

    /// <summary>Refreshes the phase visuals when the phase changes.</summary>
    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(PhaseLogActionText));
        OnPropertyChanged(nameof(CurrentPhaseIconSource));
        OnPropertyChanged(nameof(CurrentPhaseVisualSource));
        OnPropertyChanged(nameof(CurrentPhaseUsesAnimatedVisual));
        RefreshGuidanceContent(value, UseDarkTheme);
    }

    /// <summary>Refreshes the summary when the cycle day changes.</summary>
    partial void OnCurrentDayChanged(int value)
    {
        NotifyCycleSummaryProperties();
    }

    /// <summary>Refreshes the summary when the cycle length changes.</summary>
    partial void OnCycleLengthChanged(int value)
    {
        NotifyCycleSummaryProperties();
    }

    /// <summary>Refreshes the summary when the countdown changes.</summary>
    partial void OnDaysUntilPeriodChanged(int value)
    {
        OnPropertyChanged(nameof(PeriodCountdownText));
    }

    /// <summary>Refreshes the summary when a cycle starts or ends.</summary>
    partial void OnHasActiveCycleChanged(bool value)
    {
        NotifyCycleSummaryProperties();
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        AdvancePhaseCommand.NotifyCanExecuteChanged();
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refreshes the hint flag when the text changes.</summary>
    partial void OnCalendarEditHintTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasCalendarEditHint));
    }

    /// <summary>Refreshes the status flag when the text changes.</summary>
    partial void OnPhaseEditStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasPhaseEditStatus));
    }

    /// <summary>Raises the summary properties the page binds.</summary>
    private void NotifyCycleSummaryProperties()
    {
        OnPropertyChanged(nameof(CycleDaySummary));
        OnPropertyChanged(nameof(PeriodCountdownText));
        OnPropertyChanged(nameof(PhaseLogActionText));
        OnPropertyChanged(nameof(PredictionSummary));
    }

}
