namespace MuscleCuties.Core.Services.Dashboard;

public interface IDashboardCalendarDataService
{
    Task<IReadOnlySet<DateTime>> GetWorkoutDatesAsync(int userId, DateTime startDate, DateTime endDate);
    Task<IReadOnlySet<DateTime>> GetMealDatesAsync(int userId, DateTime startDate, DateTime endDate);
    Task<IReadOnlySet<int>> GetPlannedWorkoutWeekdaysAsync(int userId);
}
