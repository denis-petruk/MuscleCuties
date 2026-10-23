using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.UI.Workout;

namespace MuscleCuties.Core.Services.Workout;

public interface IWorkoutScheduleService
{
    Task<IReadOnlyList<ScheduleDayItem>> GetDatedScheduleAsync(
        int userId,
        DateTime startDate,
        int weeksCount = 4,
        CyclePhase? phase = null);

    Task<IReadOnlyList<ScheduleDayItem>> GetDatedScheduleForLoadedPlanAsync(
        int userId,
        DateTime startDate,
        int weeksCount,
        IReadOnlyList<WorkoutDay> workoutDays);

    Task RescheduleWorkoutAsync(
        int userId,
        DateTime sourceDate,
        DateTime targetDate);

    Task RescheduleWorkoutDayAsync(
        int userId,
        int workoutDayId,
        DateTime targetDate);
}
