using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Cycle;

namespace MuscleCuties.UI.ViewModels.Cycle;

public partial class CycleViewModel
{
    private const int DaysPerWeek = 7;
    private const int EarliestCalendarYear = 1970;
    private const int LatestCalendarYear = 2100;
    private int _calendarLoadVersion;
    [ObservableProperty] private string _calendarNavigationStatusText = string.Empty;
    [ObservableProperty] private ObservableCollection<CycleDayItem> _calendarStripDays = new();
    [ObservableProperty] private DateTime _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private bool _isMonthGridExpanded;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;

    public string MonthExpandButtonText => IsMonthGridExpanded ? "Collapse to week" : "Expand full month";
    public string WeekRangeLabel => CalendarStripDays.Count == 0
        ? string.Empty
        : $"{CalendarStripDays[0].Date:MMM d} – {CalendarStripDays[^1].Date:MMM d}";

    public RelayCommand ToggleMonthGridExpandedCommand { get; private set; } = null!;
    public AsyncRelayCommand<CycleDayItem> SelectCalendarDayCommand { get; private set; } = null!;
    public AsyncRelayCommand PreviousMonthCommand { get; private set; } = null!;
    public AsyncRelayCommand NextMonthCommand { get; private set; } = null!;
    public AsyncRelayCommand ReturnToTodayCommand { get; private set; } = null!;
    public AsyncRelayCommand PreviousWeekCommand { get; private set; } = null!;
    public AsyncRelayCommand NextWeekCommand { get; private set; } = null!;

    public bool IsViewingCurrentMonth =>
        DisplayedMonth.Year == CurrentDate.Year && DisplayedMonth.Month == CurrentDate.Month;

    public bool HasCalendarNavigationStatus => !string.IsNullOrWhiteSpace(CalendarNavigationStatusText);

    private void InitializeCalendarCommands()
    {
        ToggleMonthGridExpandedCommand = new RelayCommand(ToggleMonthGridExpanded);
        SelectCalendarDayCommand = new AsyncRelayCommand<CycleDayItem>(SelectCalendarDayAsync);
        PreviousMonthCommand = new AsyncRelayCommand(() => ChangeDisplayedMonthAsync(-1));
        NextMonthCommand = new AsyncRelayCommand(() => ChangeDisplayedMonthAsync(1));
        ReturnToTodayCommand = new AsyncRelayCommand(ReturnToTodayAsync);
        PreviousWeekCommand = new AsyncRelayCommand(() => ChangeWeekAsync(-1));
        NextWeekCommand = new AsyncRelayCommand(() => ChangeWeekAsync(1));
    }

    private async Task ChangeWeekAsync(int offset)
    {
        var target = SelectedDate.Date.AddDays(offset * DaysPerWeek);
        if (target.Year < EarliestCalendarYear || target.Year > LatestCalendarYear)
            return;

        SelectedDate = target;
        if (target.Year != DisplayedMonth.Year || target.Month != DisplayedMonth.Month)
            DisplayedMonth = new DateTime(target.Year, target.Month, 1);

        // The visible week can cross the displayed month's edge. RefreshCycleDataAsync
        // requests the whole week, including observations in the neighboring month.
        await LoadDisplayedMonthAsync();
    }

    private async Task ChangeDisplayedMonthAsync(int offset)
    {
        var firstDay = new DateTime(DisplayedMonth.Year, DisplayedMonth.Month, 1);
        if ((offset < 0 && firstDay.Year == EarliestCalendarYear && firstDay.Month == 1) ||
            (offset > 0 && firstDay.Year == LatestCalendarYear && firstDay.Month == 12))
            return;

        DisplayedMonth = firstDay.AddMonths(offset);
        SelectedDate = DisplayedMonth;
        await LoadDisplayedMonthAsync();
    }

    private async Task ReturnToTodayAsync()
    {
        SelectedDate = CurrentDate;
        DisplayedMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        await LoadDisplayedMonthAsync();
    }

    private async Task LoadDisplayedMonthAsync()
    {
        CalendarNavigationStatusText = string.Empty;
        try
        {
            await RefreshCycleDataAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Calendar month load failed ({ex.GetType().Name}).");
            CalendarDays = new ObservableCollection<CycleDayItem>();
            CalendarStripDays = new ObservableCollection<CycleDayItem>();
            CalendarNavigationStatusText = "Could not load this month. Try another month or return to today.";
        }
    }

    partial void OnDisplayedMonthChanged(DateTime value)
    {
        OnPropertyChanged(nameof(CurrentMonthLabel));
        OnPropertyChanged(nameof(IsViewingCurrentMonth));
    }

    partial void OnCalendarNavigationStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasCalendarNavigationStatus));
    }

    partial void OnCalendarStripDaysChanged(ObservableCollection<CycleDayItem> value)
    {
        OnPropertyChanged(nameof(WeekRangeLabel));
    }

    private void ToggleMonthGridExpanded()
    {
        IsMonthGridExpanded = !IsMonthGridExpanded;
        OnPropertyChanged(nameof(MonthExpandButtonText));
    }

    private async Task SelectCalendarDayAsync(CycleDayItem? day)
    {
        if (day?.Date is null) return;

        SelectedDate = day.Date.Value;
        var movedToAnotherMonth =
            SelectedDate.Year != DisplayedMonth.Year || SelectedDate.Month != DisplayedMonth.Month;
        var movedToAnotherWeek = CalendarStripDays.All(item => item.Date?.Date != SelectedDate.Date);
        if (movedToAnotherMonth)
            DisplayedMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);

        if (movedToAnotherMonth || movedToAnotherWeek)
            await LoadDisplayedMonthAsync();
        else
        {
            SyncCalendarSelection();
            RefreshCalendarStrip(CalendarDays, SelectedDate, CurrentDate, UseDarkTheme);
        }

        OpenQuickLog(CalendarStripDays.FirstOrDefault(item => item.Date?.Date == SelectedDate.Date) ?? day);
    }

    private void SyncCalendarSelection()
    {
        var target = SelectedDate.Date;
        foreach (var day in CalendarDays)
            day.IsSelected = day.Date?.Date == target;

        foreach (var day in CalendarStripDays)
            day.IsSelected = day.Date?.Date == target;
    }

    private void RefreshCalendarStrip(IReadOnlyList<CycleDayItem> allMonthDays, DateTime anchorDate, DateTime today,
        bool useDarkTheme)
    {
        CalendarStripDays = CycleCalendarItemBuilder.BuildCalendarStrip(
            allMonthDays,
            anchorDate,
            today,
            SelectedDate,
            useDarkTheme,
            date => _cachedPrediction is not null
                ? CreateCalendarDayItem(
                    date,
                    _cachedPrediction,
                    _phaseLogs,
                    _cycleHistory,
                    _symptomLogs,
                    today,
                    SelectedDate,
                    useDarkTheme)
                : null);
    }
}
