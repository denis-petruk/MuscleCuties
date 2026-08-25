namespace MuscleCuties.Core.Services.Workout;

public sealed record WorkoutExerciseLogInput(
    int WorkoutDayExerciseId,
    int ExerciseId,
    int CompletedSets,
    int CompletedReps,
    float? WeightKg,
    int? CompletedDurationSeconds = null,
    float? DistanceKm = null,
    int? AverageHeartRateBpm = null,
    int? PaceSecondsPerKm = null);
