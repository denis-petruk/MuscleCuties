using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

internal static class WorkoutDurationEstimator
{
    private const int StrengthWarmupMinutes = 12;
    private const int StrengthTransitionMinutes = 3;
    private const int StrengthSecondsPerRep = 4;

    public static int EstimateStrengthMinutes(IEnumerable<WorkoutDayExercise> workoutExercises)
    {
        var exercises = workoutExercises
            .Where(exercise => exercise.Sets > 0 && exercise.Reps > 0)
            .ToList();
        if (exercises.Count == 0)
            return 45;

        var workSeconds = exercises.Sum(exercise => exercise.Sets * exercise.Reps * StrengthSecondsPerRep);
        var restSeconds = exercises.Sum(exercise => Math.Max(0, exercise.Sets - 1) * BuildRestSeconds(exercise));
        var transitionSeconds = Math.Max(0, exercises.Count - 1) * StrengthTransitionMinutes * 60;
        var warmupSeconds = StrengthWarmupMinutes * 60;

        var minutes = (int)Math.Ceiling((workSeconds + restSeconds + transitionSeconds + warmupSeconds) / 60d);
        return Math.Max(45, minutes);
    }

    private static int BuildRestSeconds(WorkoutDayExercise exercise)
    {
        if (exercise.Reps <= 5)
            return 150;

        if (exercise.Reps <= 8)
            return 150;

        if (exercise.Reps <= 10)
            return 120;

        return 90;
    }
}
