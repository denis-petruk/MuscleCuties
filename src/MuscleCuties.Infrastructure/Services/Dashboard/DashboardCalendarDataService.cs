using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Workout;

namespace MuscleCuties.Core.Services.Dashboard;

public sealed class DashboardCalendarDataService(
    IWorkoutRepository workouts,
    INutritionRepository nutrition) : IDashboardCalendarDataService
{
    public async Task<IReadOnlySet<DateTime>> GetWorkoutDatesAsync(
        int userId, DateTime startDate, DateTime endDate)
    {
        var logs = await workouts.GetWorkoutLogsByDateRangeAsync(userId, startDate, endDate);
        return logs.Select(log => log.Date.Date).ToHashSet();
    }

    public async Task<IReadOnlySet<DateTime>> GetMealDatesAsync(
        int userId, DateTime startDate, DateTime endDate)
    {
        var meals = await nutrition.GetLoggedMealsByDateRangeAsync(userId, startDate, endDate);
        return meals.Select(meal => meal.Date.Date).ToHashSet();
    }

    public async Task<IReadOnlySet<int>> GetPlannedWorkoutWeekdaysAsync(int userId)
    {
        var plan = await workouts.GetActivePlanAsync(userId);
        if (plan is null)
            return new HashSet<int>();

        var days = await workouts.GetWorkoutDaysByPlanAsync(plan.Id);
        return days.Select(day => day.DayOfWeek).ToHashSet();
    }
}
