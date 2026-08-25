using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public sealed record WorkoutPlanSummary(
    WorkoutPlan? ActivePlan,
    IReadOnlyList<WorkoutDay> WorkoutDays,
    IReadOnlyList<WorkoutListItem> Workouts);
