using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IWorkoutPlanner
{
    IReadOnlyList<WorkoutItem> BuildWorkoutItems(
        IReadOnlyCollection<WorkoutDay> workoutDays,
        IReadOnlyCollection<WorkoutLog>? workoutLogs = null);

    TodaysWorkoutSummary BuildTodaysSummary(
        WorkoutPlan? plan,
        IReadOnlyCollection<WorkoutDay> workoutDays,
        IReadOnlyCollection<WorkoutLog> workoutLogs,
        CyclePhase phase,
        DateTime date);
}
