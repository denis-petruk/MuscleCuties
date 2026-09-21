using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Dashboard;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Cycle.Planning;

namespace MuscleCuties.Core.ViewModels.Dashboard;

public partial class DashboardViewModel
{
    [ObservableProperty] private ObservableCollection<DashboardCalendarDay> _calendarDays = [];
    [ObservableProperty] private string _calendarMonthLabel = string.Empty;

    private DateTime _calendarViewMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private int _calendarUserId;
    private CyclePrediction? _calendarPrediction;

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        _calendarViewMonth = _calendarViewMonth.AddMonths(-1);
        await BuildCalendarAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        _calendarViewMonth = _calendarViewMonth.AddMonths(1);
        await BuildCalendarAsync();
    }

    private async Task BuildCalendarAsync()
    {
        var userId = _calendarUserId;
        var prediction = _calendarPrediction;
        var viewMonth = _calendarViewMonth;

        CalendarMonthLabel = viewMonth.ToString("MMMM yyyy");

        var firstOfMonth = new DateTime(viewMonth.Year, viewMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(viewMonth.Year, viewMonth.Month);

        // Monday-based offset (Monday=0 ... Sunday=6)
        var firstDayOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var totalCells = firstDayOffset + daysInMonth;
        var rowCount = (int)Math.Ceiling(totalCells / 7.0);
        totalCells = rowCount * 7;

        var rangeStart = firstOfMonth.AddDays(-firstDayOffset);
        var rangeEnd = rangeStart.AddDays(totalCells - 1);

        // Load workout logs and meals for the date range
        HashSet<DateTime> workoutDates = [];
        HashSet<int> plannedWorkoutDaysOfWeek = [];
        HashSet<DateTime> mealDates = [];

        try
        {
            var logsTask = RunScopedAsync(services =>
                services.GetRequiredService<IWorkoutRepository>()
                    .GetWorkoutLogsByDateRangeAsync(userId, rangeStart, rangeEnd));

            var mealsTask = RunScopedAsync(services =>
                services.GetRequiredService<INutritionRepository>()
                    .GetLoggedMealsByDateRangeAsync(userId, rangeStart, rangeEnd));

            var planTask = RunScopedAsync(async services =>
            {
                var repo = services.GetRequiredService<IWorkoutRepository>();
                var plan = await repo.GetActivePlanAsync(userId);
                if (plan is null) return new List<Models.Entities.Workout.WorkoutDay>();
                return await repo.GetWorkoutDaysByPlanAsync(plan.Id);
            });

            await Task.WhenAll(logsTask, mealsTask, planTask);

            var logs = await logsTask;
            foreach (var log in logs)
                workoutDates.Add(log.Date.Date);

            var meals = await mealsTask;
            foreach (var meal in meals)
                mealDates.Add(meal.Date.Date);

            var planDays = await planTask;
            foreach (var day in planDays)
                plannedWorkoutDaysOfWeek.Add(day.DayOfWeek);
        }
        catch (Exception exception)
        {
            // Calendar is non-critical — show empty dots on failure
            Trace.WriteLine($"[Dashboard] Calendar data load failed: {exception}");
        }

        var today = DateTime.Today;
        var days = new List<DashboardCalendarDay>(totalCells);

        for (var i = 0; i < totalCells; i++)
        {
            var date = rangeStart.AddDays(i);
            var isCurrentMonth = date.Month == viewMonth.Month && date.Year == viewMonth.Year;
            var isToday = date.Date == today;

            var phaseColor = GetPhaseColorForDate(date, prediction, UseDarkTheme);
            var dotDayOfWeek = (int)date.DayOfWeek;

            days.Add(new DashboardCalendarDay
            {
                Day = date.Day,
                IsCurrentMonth = isCurrentMonth,
                IsToday = isToday,
                PhaseColor = phaseColor,
                HasWorkout = workoutDates.Contains(date.Date),
                HasPlannedWorkout = isCurrentMonth && plannedWorkoutDaysOfWeek.Contains(dotDayOfWeek)
                                    && !workoutDates.Contains(date.Date)
                                    && date.Date >= today,
                HasMealLogged = mealDates.Contains(date.Date)
            });
        }

        CalendarDays = new ObservableCollection<DashboardCalendarDay>(days);
    }

    private static Color GetPhaseColorForDate(DateTime date, CyclePrediction? prediction, bool isDark)
    {
        if (prediction is null)
            return Colors.Transparent;

        if (!prediction.HasActiveCycle &&
            !IsManualPhasePrediction(prediction) &&
            !IsProfilePhasePrediction(prediction))
            return Colors.Transparent;

        DateTime cycleStart;
        if (prediction.CurrentCycleStartDate is not null)
        {
            cycleStart = prediction.CurrentCycleStartDate.Value.Date;
        }
        else if (prediction.CurrentDay > 0)
        {
            cycleStart = DateTime.Today.AddDays(-(prediction.CurrentDay - 1));
        }
        else
        {
            return Colors.Transparent;
        }

        var cycleLength = CyclePhaseRules.NormalizeCycleLength(prediction.PredictedCycleLength);
        var daysDiff = (date.Date - cycleStart).Days;
        var cycleDay = (daysDiff % cycleLength + cycleLength) % cycleLength + 1;

        var phase = CyclePhaseRules.CalculatePhase(cycleDay, cycleLength);

        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(isDark ? "#5A3840" : "#F9D6D8"),
            CyclePhase.Follicular => Color.FromArgb(isDark ? "#2E5230" : "#D6EED6"),
            CyclePhase.Ovulatory => Color.FromArgb(isDark ? "#5A4A00" : "#FFF0C4"),
            CyclePhase.Luteal => Color.FromArgb(isDark ? "#3E2A58" : "#E8D8F5"),
            _ => Colors.Transparent
        };
    }
}
