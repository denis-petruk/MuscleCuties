using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Cycle;

namespace MuscleCuties.UI.ViewModels.Cycle;

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

    public string MonthExpandButtonText => IsMonthGridExpanded ? "Hide month grid" : "Show month grid";

    public RelayCommand ToggleMonthGridExpandedCommand { get; private set; } = null!;
    public AsyncRelayCommand<CycleDayItem> SelectCalendarDayCommand { get; private set; } = null!;
    public AsyncRelayCommand PreviousMonthCommand { get; private set; } = null!;
    public AsyncRelayCommand NextMonthCommand { get; private set; } = null!;
    public AsyncRelayCommand ReturnToTodayCommand { get; private set; } = null!;

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
        if (movedToAnotherMonth)
            DisplayedMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);

        if (movedToAnotherMonth)
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
        CalendarStripDays = new ObservableCollection<CycleDayItem>(allMonthDays
            .Where(day => day.Date is not null)
            .Select(day => CycleCalendarItemBuilder.BuildStripDayItem(
                day.Date!.Value, day, today, anchorDate, useDarkTheme)));
    }
}
