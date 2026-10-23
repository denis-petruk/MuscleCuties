using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

internal static class WorkoutDurationEstimator
{
    public static int EstimateStrengthMinutes(IEnumerable<WorkoutDayExercise> workoutExercises)
    {
        return WorkoutDurationRule.EstimateStrengthMinutes(workoutExercises?
            .Where(exercise => exercise is not null)
            .Select(exercise => new WorkoutExerciseDose(exercise.Sets, exercise.Reps))
            .ToArray());
    }
}
