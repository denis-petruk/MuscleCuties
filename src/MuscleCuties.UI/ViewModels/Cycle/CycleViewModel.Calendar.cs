// Calendar strip, day selection, and the phase shown for a chosen day.
// Second half of CycleViewModel; see its declaration for the full contract.

using MuscleCuties.UI.Models.UI.Cycle;

namespace MuscleCuties.UI.ViewModels.Cycle;

/// <summary>The calendar half of the cycle page: the month grid, its month-to-month navigation and the day the page is</summary>
/// focused on.</summary>
public partial class CycleViewModel
{
    private const int EarliestCalendarYear = 1970;
    private const int LatestCalendarYear = 2100;
    private int _calendarLoadVersion;
    [ObservableProperty] private string _calendarNavigationStatusText = string.Empty;
    [ObservableProperty] private ObservableCollection<CycleDayItem> _calendarStripDays = new();
    [ObservableProperty] private DateTime _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private bool _isMonthGridExpanded;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;

    /// <summary>The grid control names the action it will take, expand or collapse.</summary>
    public string MonthExpandButtonText => IsMonthGridExpanded ? "Hide month grid" : "Show month grid";

    /// <summary>Shows the full month grid or folds it back to the summary.</summary>
    public RelayCommand ToggleMonthGridExpandedCommand { get; private set; } = null!;
    /// <summary>Opens the tapped day.</summary>
    public AsyncRelayCommand<CycleDayItem> SelectCalendarDayCommand { get; private set; } = null!;
    /// <summary>Steps the calendar one month back.</summary>
    public AsyncRelayCommand PreviousMonthCommand { get; private set; } = null!;
    /// <summary>Steps the calendar one month forward.</summary>
    public AsyncRelayCommand NextMonthCommand { get; private set; } = null!;
    /// <summary>Returns the calendar to today.</summary>
    public AsyncRelayCommand ReturnToTodayCommand { get; private set; } = null!;

    /// <summary>True when the displayed month is the current one.</summary>
    public bool IsViewingCurrentMonth =>
        DisplayedMonth.Year == CurrentDate.Year && DisplayedMonth.Month == CurrentDate.Month;

    /// <summary>True when a calendar navigation status line is shown.</summary>
    public bool HasCalendarNavigationStatus => !string.IsNullOrWhiteSpace(CalendarNavigationStatusText);

    /// <summary>Wires the calendar's commands to their handlers.</summary>
    private void InitializeCalendarCommands()
    {
        ToggleMonthGridExpandedCommand = new RelayCommand(ToggleMonthGridExpanded);
        SelectCalendarDayCommand = new AsyncRelayCommand<CycleDayItem>(SelectCalendarDayAsync);
        PreviousMonthCommand = new AsyncRelayCommand(() => ChangeDisplayedMonthAsync(-1));
        NextMonthCommand = new AsyncRelayCommand(() => ChangeDisplayedMonthAsync(1));
        ReturnToTodayCommand = new AsyncRelayCommand(ReturnToTodayAsync);
    }

    /// <summary>Moves the grid by whole months and loads the month it lands on.</summary>
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

    /// <summary>Comes back to the month that holds today and selects today, so a user lost in past months has one way</summary>
    /// back.</summary>
    private async Task ReturnToTodayAsync()
    {
        SelectedDate = CurrentDate;
        DisplayedMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        await LoadDisplayedMonthAsync();
    }

    /// <summary>Rebuilds the grid's cells for the month on screen, from the logs already loaded.</summary>
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
            CalendarNavigationStatusText =
                MuscleCuties.UI.Services.Localization.UiTextCatalog.Get("Cycle_Calendar_LoadFailed");
        }
    }

    /// <summary>Rebuilds the strip when the displayed month changes.</summary>
    partial void OnDisplayedMonthChanged(DateTime value)
    {
        OnPropertyChanged(nameof(CurrentMonthLabel));
        OnPropertyChanged(nameof(IsViewingCurrentMonth));
    }

    /// <summary>Refreshes the status flag when the text changes.</summary>
    partial void OnCalendarNavigationStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasCalendarNavigationStatus));
    }

    /// <summary>Flips between the expanded and folded grid.</summary>
    private void ToggleMonthGridExpanded()
    {
        IsMonthGridExpanded = !IsMonthGridExpanded;
        OnPropertyChanged(nameof(MonthExpandButtonText));
    }

    /// <summary>Focuses a day and opens its quick log, refusing while another load or write is running.</summary>
    private async Task SelectCalendarDayAsync(CycleDayItem? day)
    {
        if (day?.Date is null) return;

        if (!IsDatePhaseModalVisible)
            _selectedDateBeforeCalendarEdit = SelectedDate;

        SelectedDate = day.Date.Value;
        var movedToAnotherMonth =
            SelectedDate.Year != DisplayedMonth.Year || SelectedDate.Month != DisplayedMonth.Month;
        if (movedToAnotherMonth)
            DisplayedMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);

        if (movedToAnotherMonth)
            await LoadDisplayedMonthAsync();
        else
        {
            SyncCalendarSelection();
            RefreshCalendarStrip(CalendarDays, SelectedDate, CurrentDate, UseDarkTheme);
        }

        OpenCalendarDay(CalendarStripDays.FirstOrDefault(item => item.Date?.Date == SelectedDate.Date) ?? day);
    }

    /// <summary>Marks the focused day's cell, so the grid agrees with the summary above it.</summary>
    private void SyncCalendarSelection()
    {
        var target = SelectedDate.Date;
        foreach (var day in CalendarDays)
            day.IsSelected = day.Date?.Date == target;

        foreach (var day in CalendarStripDays)
            day.IsSelected = day.Date?.Date == target;
    }

    /// <summary>Rebuilds the calendar strip for the anchor date.</summary>
    private void RefreshCalendarStrip(IReadOnlyList<CycleDayItem> allMonthDays, DateTime anchorDate, DateTime today,
        bool useDarkTheme)
    {
        CalendarStripDays = new ObservableCollection<CycleDayItem>(allMonthDays
            .Where(day => day.Date is not null)
            .Select(day => CycleCalendarItemBuilder.BuildStripDayItem(
                day.Date!.Value, day, today, anchorDate, useDarkTheme)));
    }
}
