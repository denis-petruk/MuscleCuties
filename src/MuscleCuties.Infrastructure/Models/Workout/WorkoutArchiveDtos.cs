namespace MuscleCuties.Core.Models.Workout;

/// <summary>One archived set: the exercise, the reps and weight performed, and when.</summary>
public sealed record ExerciseSetLogDto(
    Guid Id,
    Guid WorkoutSessionLogId,
    string? ExerciseId,
    string ExerciseName,
    int SetNumber,
    double WeightKg,
    int RepsCompleted,
    double? Rpe,
    bool IsWarmup,
    int RestTimeSeconds);
