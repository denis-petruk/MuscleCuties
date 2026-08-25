using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Cycle;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Cycle;

public partial class CycleViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;
    private readonly IUserRepository _userRepository;
    private readonly Action<CyclePhase> _openPhaseDetails;
    private readonly ViewModelLoadGate _loadGate = new(TimeSpan.FromSeconds(20));
    private readonly List<CyclePhaseLog> _phaseLogs = new();

    private CycleDayItem? _selectedCalendarDay;
    private CyclePhase? _suggestedPhase;
    private CyclePhase? _cycleWarningSuggestedPhase;

    [ObservableProperty] private CyclePhase _currentPhase;
    [ObservableProperty] private int _currentDay;
    [ObservableProperty] private int _cycleLength;
    [ObservableProperty] private int _daysUntilPeriod;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasActiveCycle;
    [ObservableProperty] private string _lastPhaseLogText = "No shift logged yet";
    [ObservableProperty] private string _calendarEditHintText = string.Empty;
    [ObservableProperty] private bool _isDatePhaseModalVisible;
    [ObservableProperty] private bool _hasPhaseJumpWarning;
    [ObservableProperty] private bool _useDarkTheme;
    [ObservableProperty] private string _selectedCalendarDateText = string.Empty;
    [ObservableProperty] private string _selectedCalendarPhaseText = string.Empty;
    [ObservableProperty] private string _phaseEditStatusText = string.Empty;
    [ObservableProperty] private string _phaseJumpWarningTitle = string.Empty;
    [ObservableProperty] private string _phaseJumpWarningText = string.Empty;
    [ObservableProperty] private bool _isCycleWarningPopupVisible;
    [ObservableProperty] private string _cycleWarningTitle = string.Empty;
    [ObservableProperty] private string _cycleWarningText = string.Empty;
    [ObservableProperty] private string _cycleWarningSuggestedActionText = "Use next phase";
    [ObservableProperty] private ObservableCollection<CycleDayItem> _calendarDays = new();
    [ObservableProperty] private ObservableCollection<CyclePhaseOptionItem> _phaseEditOptions = new();
    [ObservableProperty] private ObservableCollection<PhaseItem> _phases = new();

    public string PhaseLabel => CurrentPhase.ToString();
    public string CurrentMonthLabel => DateTime.Today.ToString("MMMM yyyy");
    public string CycleDaySummary => HasActiveCycle ? $"Day {CurrentDay} of {CycleLength}" : "Start cycle tracking";
    public string PeriodCountdownText => HasActiveCycle
        ? DaysUntilPeriod == 0 ? "Period due today" : $"Period in {DaysUntilPeriod}d"
        : "Log period start";
    public string PhaseLogActionText => HasActiveCycle ? "Next phase" : "Start cycle";
    public string PredictionSummary => HasActiveCycle ? $"Prediction from {CycleLength}d cycle" : "No active cycle yet";
    public DateTime Today => DateTime.Today;
    public bool HasCalendarEditHint => !string.IsNullOrWhiteSpace(CalendarEditHintText);
    public bool HasPhaseEditStatus => !string.IsNullOrWhiteSpace(PhaseEditStatusText);
    public bool HasSuggestedPhase => _suggestedPhase is not null;
    public bool HasCycleWarningSuggestedPhase => _cycleWarningSuggestedPhase is not null;
    public IReadOnlyList<CyclePhase> PhaseOptions { get; } = Enum.GetValues<CyclePhase>();

    // Legacy alias kept for existing tests
    public int CycleDay => CurrentDay;

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand AdvancePhaseCommand { get; }
    public RelayCommand<CycleDayItem> OpenCalendarDayCommand { get; }
    public RelayCommand CloseDatePhaseModalCommand { get; }
    public RelayCommand<CyclePhaseOptionItem> SelectPhaseOptionCommand { get; }
    public AsyncRelayCommand SaveDatePhaseCommand { get; }
    public RelayCommand UseSuggestedPhaseCommand { get; }
    public RelayCommand ReviewEarlierPhaseRecordsCommand { get; }
    public RelayCommand CloseCycleWarningPopupCommand { get; }
    public AsyncRelayCommand UseCycleWarningSuggestedPhaseCommand { get; }
    public RelayCommand<PhaseItem> OpenPhaseDetailsCommand { get; }

    public CycleViewModel(
        IAuthService authService,
        ICycleService cycleService,
        IUserRepository userRepository,
        Action<CyclePhase>? openPhaseDetails = null)
    {
        _authService = authService;
        _cycleService = cycleService;
        _userRepository = userRepository;
        _openPhaseDetails = openPhaseDetails ?? (_ => { });
        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadDataCoreAsync));
        AdvancePhaseCommand = new AsyncRelayCommand(AdvancePhaseAsync, CanAdvancePhase);
        OpenCalendarDayCommand = new RelayCommand<CycleDayItem>(OpenCalendarDay);
        CloseDatePhaseModalCommand = new RelayCommand(CloseDatePhaseModal);
        SelectPhaseOptionCommand = new RelayCommand<CyclePhaseOptionItem>(SelectPhaseOption);
        SaveDatePhaseCommand = new AsyncRelayCommand(SaveDatePhaseAsync, CanSaveDatePhase);
        UseSuggestedPhaseCommand = new RelayCommand(UseSuggestedPhase);
        ReviewEarlierPhaseRecordsCommand = new RelayCommand(ReviewEarlierPhaseRecords);
        CloseCycleWarningPopupCommand = new RelayCommand(CloseCycleWarningPopup);
        UseCycleWarningSuggestedPhaseCommand = new AsyncRelayCommand(UseCycleWarningSuggestedPhaseAsync);
        OpenPhaseDetailsCommand = new RelayCommand<PhaseItem>(OpenPhaseDetails);
        Phases = BuildPhaseItems(UseDarkTheme);
    }

    public void RefreshThemeColors(bool useDarkTheme)
    {
        if (UseDarkTheme == useDarkTheme)
        {
            ApplyThemeColors();
            return;
        }

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
        var userId = await _authService.GetCurrentUserIdAsync();
        var prediction = await _cycleService.GetPredictionAsync(userId);
        var user = await _userRepository.GetByIdAsync(userId);
        var accountCreatedDate = user?.CreatedAt.Date ?? DateTime.Today;

        HasActiveCycle = prediction.HasActiveCycle;
        CurrentDay = prediction.CurrentDay;
        CycleLength = prediction.PredictedCycleLength;
        DaysUntilPeriod = prediction.DaysUntilPeriod;
        CurrentPhase = prediction.CurrentPhase;

        var phaseLogs = await _cycleService.GetRecentPhaseLogsAsync(userId, 120);
        _phaseLogs.Clear();
        _phaseLogs.AddRange(phaseLogs);
        var latestPhaseLog = phaseLogs.FirstOrDefault();
        CalendarDays = BuildCalendarDays(prediction, phaseLogs, accountCreatedDate, UseDarkTheme);
        LastPhaseLogText = FormatLastPhaseLog(latestPhaseLog);
        Phases = BuildPhaseItems(UseDarkTheme);

        OnPropertyChanged(nameof(Today));
        OnPropertyChanged(nameof(PhaseLabel));
        NotifyCycleSummaryProperties();
    }

    private async Task AdvancePhaseAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var nextPhase = HasActiveCycle
                ? CyclePhaseRules.GetNextPhase(CurrentPhase)
                : CyclePhase.Menstrual;
            await _cycleService.SetPhaseForDateAsync(userId, nextPhase, DateTime.Today, "Manual phase advance");
            await RefreshCycleDataAsync();
        }
        catch (CyclePhaseOrderException ex)
        {
            ShowCycleOrderPopup(ex.Message, ex.SuggestedPhase);
        }
        catch (InvalidOperationException ex)
        {
            ShowCycleWarningPopup("Could not change phase yet", FormatCycleOrderWarningMessage(ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenCalendarDay(CycleDayItem? day)
    {
        if (day?.Date is null)
            return;

        _selectedCalendarDay = day;
        _suggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        HasPhaseJumpWarning = false;
        PhaseEditStatusText = string.Empty;
        CalendarEditHintText = string.Empty;
        SelectedCalendarDateText = day.Date.Value.ToString("MMMM d, yyyy");
        SelectedCalendarPhaseText = day.IsNeutral
            ? "Neutral, ready when you are"
            : $"{day.Phase} phase";
        PhaseEditOptions = BuildPhaseOptions(day.IsNeutral ? null : day.Phase, UseDarkTheme);
        IsDatePhaseModalVisible = true;
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
    }

    private void OpenPhaseDetails(PhaseItem? phase)
    {
        if (phase is null)
            return;

        _openPhaseDetails(phase.Phase);
    }

    private void CloseDatePhaseModal()
    {
        IsDatePhaseModalVisible = false;
        HasPhaseJumpWarning = false;
        PhaseEditStatusText = string.Empty;
        _selectedCalendarDay = null;
        _suggestedPhase = null;
        OnPropertyChanged(nameof(HasSuggestedPhase));
        SaveDatePhaseCommand.NotifyCanExecuteChanged();
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

        var orderWarning = BuildPhaseOrderWarning(date, selectedPhase.Value);
        if (orderWarning is not null)
        {
            _suggestedPhase = orderWarning.Value.SuggestedPhase;
            OnPropertyChanged(nameof(HasSuggestedPhase));
            PhaseJumpWarningTitle = orderWarning.Value.Title;
            PhaseJumpWarningText = orderWarning.Value.Message;
            HasPhaseJumpWarning = true;
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
        PhaseEditStatusText = $"{FormatPhaseName(_suggestedPhase.Value)} keeps the cycle in order. Tap Save phase to log it.";
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

    private bool CanSaveDatePhase() => _selectedCalendarDay?.Date is not null;

    private async Task SavePhaseForDateAsync(DateTime date, CyclePhase phase, string note)
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            await _cycleService.SetPhaseForDateAsync(userId, phase, date, note);
            CloseDatePhaseModal();
            await RefreshCycleDataAsync();
        }
        catch (CyclePhaseOrderException ex)
        {
            ShowPhaseOrderWarning(ex.Message, ex.SuggestedPhase);
        }
        catch (InvalidOperationException ex)
        {
            PhaseJumpWarningTitle = "This needs a quick fix";
            PhaseJumpWarningText = ex.Message;
            HasPhaseJumpWarning = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private CyclePhase? GetSelectedPhase() =>
        PhaseEditOptions.FirstOrDefault(option => option.IsSelected)?.Phase;

    private bool CanAdvancePhase() => !IsBusy;

    private static string FormatLastPhaseLog(CyclePhaseLog? log) =>
        log is null ? "No shift logged yet" : $"{log.Phase} logged {log.LoggedAt:MMM d}";

    private static ObservableCollection<CycleDayItem> BuildCalendarDays(
        CyclePrediction prediction,
        IReadOnlyCollection<CyclePhaseLog> phaseLogs,
        DateTime accountCreatedDate,
        bool useDarkTheme)
    {
        var items = new ObservableCollection<CycleDayItem>();
        var today = DateTime.Today;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var cycleLength = CyclePhaseRules.NormalizeCycleLength(prediction.PredictedCycleLength);
        var cycleStartDate = prediction.CurrentCycleStartDate?.Date;
        var orderedPhaseLogs = phaseLogs
            .OrderBy(log => log.LoggedAt)
            .ThenBy(log => log.CreatedAt)
            .ToList();

        for (var dayOfMonth = 1; dayOfMonth <= daysInMonth; dayOfMonth++)
        {
            var date = firstDayOfMonth.AddDays(dayOfMonth - 1);
            var cycleDay = ResolveCycleDay(date, cycleStartDate, cycleLength);
            var isToday = date.Date == today.Date;
            var hasPhaseShiftLog = orderedPhaseLogs.Any(log => log.LoggedAt.Date == date.Date);
            var hasHistoryForDate = orderedPhaseLogs.Any(log => log.LoggedAt.Date <= date.Date);
            var isNeutral = date.Date < accountCreatedDate.Date && !hasHistoryForDate;
            var isPredictedFuture = date.Date > today.Date && !hasPhaseShiftLog && !isNeutral;
            var phase = isNeutral
                ? prediction.CurrentPhase
                : ResolveCalendarPhase(cycleDay, date, cycleLength, prediction, orderedPhaseLogs);

            items.Add(new CycleDayItem
            {
                Day = dayOfMonth,
                CycleDay = cycleDay,
                Date = date,
                Phase = phase,
                IsToday = isToday,
                IsNeutral = isNeutral,
                HasPhaseShiftLog = hasPhaseShiftLog,
                IsPredictedFuture = isPredictedFuture,
                BackgroundColor = isNeutral ? GetNeutralDayBackground(isToday, useDarkTheme) : GetDayBackground(phase, isToday, useDarkTheme),
                TextColor = isToday ? GetTodayDayTextColor(useDarkTheme) : isNeutral ? GetNeutralDayTextColor(useDarkTheme) : GetPhaseTextColor(phase, useDarkTheme),
                StrokeColor = ResolveDayStrokeColor(phase, isToday, hasPhaseShiftLog, isPredictedFuture, useDarkTheme),
                StrokeThickness = isToday ? 2 : hasPhaseShiftLog ? 1.25 : isPredictedFuture ? 1.15 : 0
            });
        }
        return items;
    }

    private static Color ResolveDayStrokeColor(
        CyclePhase phase,
        bool isToday,
        bool hasPhaseShiftLog,
        bool isPredictedFuture,
        bool useDarkTheme)
    {
        if (isToday || hasPhaseShiftLog)
            return GetPhaseTextColor(phase, useDarkTheme);

        return isPredictedFuture ? GetPredictedDayStrokeColor(phase, useDarkTheme) : Colors.Transparent;
    }

    private static int ResolveCycleDay(DateTime date, DateTime? cycleStartDate, int cycleLength)
    {
        if (cycleStartDate is null)
            return 0;

        var daysFromCycleStart = (date.Date - cycleStartDate.Value.Date).Days;
        var normalizedOffset = ((daysFromCycleStart % cycleLength) + cycleLength) % cycleLength;
        return normalizedOffset + 1;
    }

    private static CyclePhase ResolveCalendarPhase(
        int cycleDay,
        DateTime date,
        int cycleLength,
        CyclePrediction prediction,
        IReadOnlyList<CyclePhaseLog> orderedPhaseLogs)
    {
        var latestShift = orderedPhaseLogs
            .Where(log => log.LoggedAt.Date <= date.Date)
            .LastOrDefault();

        if (latestShift is null)
            return cycleDay > 0
                ? CyclePhaseRules.CalculatePhase(cycleDay, cycleLength)
                : prediction.CurrentPhase;

        if (latestShift.LoggedAt.Date == date.Date)
            return latestShift.Phase;

        if (date.Date == DateTime.Today && prediction.HasActiveCycle)
            return prediction.CurrentPhase;

        return CyclePhaseRules.ProjectPhaseFromLog(
            new CyclePhaseLogProjection(latestShift.Phase, latestShift.LoggedAt),
            date,
            cycleLength);
    }

    private static Color GetDayBackground(CyclePhase phase, bool isToday, bool useDarkTheme)
    {
        if (isToday) return GetPhaseTextColor(phase, useDarkTheme);

        return GetPhaseBackgroundColor(phase, useDarkTheme);
    }

    private static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme) =>
        phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#5A3840" : "#F9D6D8"),
            CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#2E5230" : "#D6EED6"),
            CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#5A4A00" : "#FFF0C4"),
            CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#3E2A58" : "#E8D8F5"),
            _ => Colors.Transparent
        };

    private static Color GetNeutralDayBackground(bool isToday, bool useDarkTheme) =>
        isToday
            ? Color.FromArgb(useDarkTheme ? "#AE8D9B" : "#8B7E86")
            : Color.FromArgb(useDarkTheme ? "#4C3942" : "#EEF0F2");

    private static Color GetNeutralDayTextColor(bool useDarkTheme) =>
        Color.FromArgb(useDarkTheme ? "#F8EEF4" : "#8B7E86");

    private static Color GetTodayDayTextColor(bool useDarkTheme) =>
        Color.FromArgb(useDarkTheme ? "#2B1D24" : "#FFFFFF");

    private static Color GetPredictedDayStrokeColor(CyclePhase phase, bool useDarkTheme) => phase switch
    {
        CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#F9D6D8" : "#B86B78"),
        CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#D6EED6" : "#679B67"),
        CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#FFF0C4" : "#B08A00"),
        CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#E8D8F5" : "#8B6AB8"),
        _ => Color.FromArgb(useDarkTheme ? "#F8EEF4" : "#8B7E86")
    };

    private static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme) => phase switch
    {
        CyclePhase.Menstrual => Color.FromArgb(useDarkTheme ? "#F9D6D8" : "#7A3A48"),
        CyclePhase.Follicular => Color.FromArgb(useDarkTheme ? "#D6EED6" : "#3A6B3A"),
        CyclePhase.Ovulatory => Color.FromArgb(useDarkTheme ? "#FFF0C4" : "#7A6000"),
        CyclePhase.Luteal => Color.FromArgb(useDarkTheme ? "#E8D8F5" : "#5A3B80"),
        _ => Color.FromArgb(useDarkTheme ? "#F8EEF4" : "#1F2937")
    };

    private static ObservableCollection<PhaseItem> BuildPhaseItems(bool useDarkTheme) =>
        new()
        {
            new PhaseItem
            {
                Phase = CyclePhase.Menstrual,
                Name = "Menstrual",
                Description = "Days 1-5 · Low intensity, rest and recover",
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Menstrual, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Menstrual, useDarkTheme)
            },
            new PhaseItem
            {
                Phase = CyclePhase.Follicular,
                Name = "Follicular",
                Description = "Days 6-13 · Energy rising, great for strength training",
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Follicular, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Follicular, useDarkTheme)
            },
            new PhaseItem
            {
                Phase = CyclePhase.Ovulatory,
                Name = "Ovulatory",
                Description = "Days 14-16 · Peak performance, max intensity",
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Ovulatory, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Ovulatory, useDarkTheme)
            },
            new PhaseItem
            {
                Phase = CyclePhase.Luteal,
                Name = "Luteal",
                Description = "Days 17-28 · Moderate exercise, listen to your body",
                BackgroundColor = GetPhaseBackgroundColor(CyclePhase.Luteal, useDarkTheme),
                TextColor = GetPhaseTextColor(CyclePhase.Luteal, useDarkTheme)
            }
        };

    private static ObservableCollection<CyclePhaseOptionItem> BuildPhaseOptions(CyclePhase? selectedPhase, bool useDarkTheme) =>
        new(BuildPhaseItems(useDarkTheme).Select(item =>
        {
            return new CyclePhaseOptionItem
            {
                Phase = item.Phase,
                Name = item.Name,
                Description = item.Description,
                BackgroundColor = item.BackgroundColor,
                TextColor = item.TextColor,
                IsSelected = selectedPhase == item.Phase
            };
        }));

    private PhaseOrderWarning? BuildPhaseOrderWarning(DateTime date, CyclePhase selectedPhase)
    {
        var previousDay = CalendarDays.FirstOrDefault(day => day.Date?.Date == date.Date.AddDays(-1));
        var nextLog = _phaseLogs
            .Where(log => log.LoggedAt.Date > date.Date)
            .OrderBy(log => log.LoggedAt)
            .ThenBy(log => log.CreatedAt)
            .FirstOrDefault();

        if (previousDay is { IsNeutral: false } && !FollowsCycleOrder(previousDay.Phase, selectedPhase))
        {
            var expectedPhase = CyclePhaseRules.GetNextPhase(previousDay.Phase);
            return new PhaseOrderWarning(
                "Keep your rhythm clean",
                $"Yesterday was {FormatPhaseName(previousDay.Phase)}. This day can stay {FormatPhaseName(previousDay.Phase)} or move to {FormatPhaseName(expectedPhase)} so your cycle stays in order.",
                expectedPhase);
        }

        if (nextLog is not null)
        {
            var phaseBeforeNextLog = CyclePhaseRules.ProjectPhaseFromLog(
                new CyclePhaseLogProjection(selectedPhase, date),
                nextLog.LoggedAt.Date.AddDays(-1),
                CycleLength);

            if (!FollowsCycleOrder(phaseBeforeNextLog, nextLog.Phase))
            {
                var expectedPhase = CyclePhaseRules.GetNextPhase(phaseBeforeNextLog);
                return new PhaseOrderWarning(
                    "Keep your rhythm clean",
                    $"This would make the next saved shift jump out of order. Add the missing {FormatPhaseName(expectedPhase)} record first, or use it for this day.",
                    expectedPhase);
            }
        }

        return null;
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
        CycleWarningTitle = string.Empty;
        CycleWarningText = string.Empty;
        CycleWarningSuggestedActionText = "Use next phase";
        _cycleWarningSuggestedPhase = null;
        OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
    }

    private async Task UseCycleWarningSuggestedPhaseAsync()
    {
        if (_cycleWarningSuggestedPhase is null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var suggestedPhase = _cycleWarningSuggestedPhase.Value;
            await _cycleService.SetPhaseForDateAsync(userId, suggestedPhase, DateTime.Today, "Manual phase correction");
            CloseCycleWarningPopup();
            await RefreshCycleDataAsync();
        }
        catch (CyclePhaseOrderException ex)
        {
            ShowCycleRepairPopup(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _cycleWarningSuggestedPhase = null;
            CycleWarningSuggestedActionText = "Use next phase";
            OnPropertyChanged(nameof(HasCycleWarningSuggestedPhase));
            ShowCycleWarningPopup("Could not save that yet", FormatCycleOrderWarningMessage(ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool FollowsCycleOrder(CyclePhase from, CyclePhase to) =>
        to == from || to == CyclePhaseRules.GetNextPhase(from);

    private static string FormatCycleOrderWarningMessage(string message) =>
        message
            .Replace("This phase would break the cycle order.", "This phase would skip a step.")
            .Replace("Cycle phases must follow order.", "This phase would skip a step.");

    private static string FormatPhaseName(CyclePhase phase) => phase.ToString().ToLowerInvariant();

    private readonly record struct PhaseOrderWarning(string Title, string Message, CyclePhase SuggestedPhase);

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
                IsToday = day.IsToday,
                IsNeutral = day.IsNeutral,
                HasPhaseShiftLog = day.HasPhaseShiftLog,
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
                    day.HasPhaseShiftLog,
                    day.IsPredictedFuture,
                    UseDarkTheme),
                StrokeThickness = day.StrokeThickness
            }));
    }

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(PhaseLogActionText));
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
