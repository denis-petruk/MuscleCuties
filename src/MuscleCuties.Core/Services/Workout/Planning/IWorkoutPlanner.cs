using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IWorkoutPlanner
{
    bool ShouldReplaceGeneratedPlan(
        WorkoutPlan? activePlan,
        IReadOnlyCollection<WorkoutDay> activePlanDays,
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        CyclePhase phase);

    WorkoutPlan BuildGeneratedPlan(
        int userId,
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        IReadOnlyCollection<Exercise> exerciseLibrary,
        CyclePhase phase,
        DateTime createdAt);

    IReadOnlyList<WorkoutListItem> BuildWorkoutItems(
        IReadOnlyCollection<WorkoutDay> workoutDays);

    TodaysWorkoutSummary BuildTodaysSummary(
        WorkoutPlan? plan,
        IReadOnlyCollection<WorkoutDay> workoutDays,
        IReadOnlyCollection<WorkoutLog> workoutLogs,
        CyclePhase phase,
        DateTime date);
}
