using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UI.Models.UI.Dashboard;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UI.ViewModels.Dashboard;

public partial class DashboardViewModel
{
    [ObservableProperty] private ObservableCollection<DashboardCalendarDay> _calendarDays = [];
    [ObservableProperty] private string _calendarMonthLabel = string.Empty;

    private DateTime _calendarViewMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        _calendarViewMonth = SafeAddMonths(_calendarViewMonth, -1);
        await BuildCalendarAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        _calendarViewMonth = SafeAddMonths(_calendarViewMonth, 1);
        await BuildCalendarAsync();
    }

    private async Task BuildCalendarAsync()
    {
        CalendarMonthLabel = _calendarViewMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        try
        {
            var response = await _overview.LoadCalendarAsync(new DashboardCalendarRequest(
                _calendarViewMonth.Year, _calendarViewMonth.Month, DateOnly.FromDateTime(DateTime.Today)));
            CalendarDays = new ObservableCollection<DashboardCalendarDay>(response.Days.Select(day =>
                new DashboardCalendarDay
                {
                    Day = day.Date.Day,
                    IsCurrentMonth = day.IsCurrentMonth,
                    IsToday = day.IsToday,
                    PhaseColor = GetPhaseColor(day.Date.ToDateTime(TimeOnly.MinValue), UseDarkTheme),
                    HasWorkout = day.HasWorkout,
                    HasPlannedWorkout = day.HasPlannedWorkout,
                    HasMealLogged = day.HasMealLogged
                }));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Dashboard] Calendar load failed ({ex.GetType().Name}).");
            CalendarDays = [];
        }
    }

    private Color GetPhaseColor(DateTime date, bool isDark)
    {
        var supportedPrediction = HasActiveCycle ||
                                  _calendarPredictionSource.Equals("manual phase log",
                                      StringComparison.OrdinalIgnoreCase) ||
                                  _calendarPredictionSource.Equals("profile phase", StringComparison.OrdinalIgnoreCase);
        if (!supportedPrediction)
            return Colors.Transparent;
        var cycleStart = _calendarCycleStart?.ToDateTime(TimeOnly.MinValue) ??
                         (CurrentCycleDay > 0 ? DateTime.Today.AddDays(-(CurrentCycleDay - 1)) : null);
        if (cycleStart is null)
            return Colors.Transparent;
        var length = Math.Clamp(PredictedCycleLength, 21, 45);
        var daysDiff = (date.Date - cycleStart.Value.Date).Days;
        var cycleDay = (daysDiff % length + length) % length + 1;
        return CyclePhaseRule.CalculatePhase(cycleDay, length) switch
        {
            CyclePhase.Menstrual => Color.FromArgb(isDark ? "#5A3840" : "#F9D6D8"),
            CyclePhase.Follicular => Color.FromArgb(isDark ? "#2E5230" : "#D6EED6"),
            CyclePhase.Ovulatory => Color.FromArgb(isDark ? "#5A4A00" : "#FFF0C4"),
            CyclePhase.Luteal => Color.FromArgb(isDark ? "#3E2A58" : "#E8D8F5"),
            _ => Colors.Transparent
        };
    }

    private static DateTime SafeAddMonths(DateTime date, int months)
    {
        try { return date.AddMonths(months); }
        catch (ArgumentOutOfRangeException) { return months < 0 ? DateTime.MinValue : DateTime.MaxValue; }
    }
}
