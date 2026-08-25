using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout;

public interface IWorkoutService
{
    Task<WorkoutPlanSummary> GetPlanSummaryAsync(int userId, CyclePhase phase);

    Task<TodaysWorkoutSummary> GetTodaysSummaryAsync(
        int userId,
        CyclePhase phase,
        DateTime date);

    Task RegenerateActivePlanAsync(int userId, CyclePhase phase);

    Task<WorkoutSessionDetail> GetWorkoutSessionDetailAsync(int userId, int workoutDayId);

    Task LogWorkoutSessionAsync(
        int userId,
        int workoutDayId,
        IReadOnlyCollection<WorkoutExerciseLogInput> exerciseLogs,
        DateTime date);
}
