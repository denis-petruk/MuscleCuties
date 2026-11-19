namespace MuscleCuties.Core.Models.Workout;

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
