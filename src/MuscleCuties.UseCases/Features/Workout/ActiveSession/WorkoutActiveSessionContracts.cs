namespace MuscleCuties.UseCases.Features.Workout.ActiveSession;

public sealed class WorkoutActivityNoLongerSafeException : Exception
{
    public WorkoutActivityNoLongerSafeException() : base("The activity is no longer safe to continue.") { }
}

public sealed record ActiveExerciseSetInput(
    Guid SetId,
    string? ExerciseId,
    string ExerciseName,
    int SetNumber,
    decimal WeightKg,
    int RepsCompleted,
    decimal? Rpe,
    bool IsWarmup,
    int RestTimeSeconds);

public sealed record ActiveExerciseSetSnapshot(
    Guid SetId,
    string? ExerciseId,
    string ExerciseName,
    int SetNumber,
    decimal WeightKg,
    int RepsCompleted,
    decimal? Rpe,
    bool IsWarmup,
    int RestTimeSeconds,
    DateTime UpdatedAtUtc);

public sealed record ActiveWorkoutSessionSnapshot(
    Guid SessionId,
    string WorkoutName,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int TotalDurationSeconds,
    decimal TotalVolumeKg,
    string? Notes,
    int? RpeRating,
    bool IsCompleted,
    bool IsCanceled,
    IReadOnlyList<ActiveExerciseSetSnapshot> Sets,
    bool Replayed = false,
    int? PlannedWorkoutDayId = null)
{
    public int ConfirmedActiveSeconds { get; init; }
    public Guid? TimingIntervalId { get; init; }
    public int TimingIntervalConfirmedSeconds { get; init; }
}

public sealed record ResumeWorkoutTimingCommand(string IdempotencyKey, Guid SessionId);
public sealed record CheckpointWorkoutTimingCommand(string IdempotencyKey, Guid SessionId, Guid IntervalId,
    int MeasuredSeconds, bool Pause);

public sealed record StartWorkoutSessionCommand(
    string IdempotencyKey,
    Guid SessionId,
    string WorkoutName,
    DateTime StartedAtUtc,
    string? Notes = null,
    int? PlannedWorkoutDayId = null);

public sealed record UpsertWorkoutSessionSetCommand(
    string IdempotencyKey,
    Guid SessionId,
    ActiveExerciseSetInput Set);

public sealed record FinishWorkoutSessionCommand(
    string IdempotencyKey,
    Guid SessionId,
    DateTime CompletedAtUtc,
    int TotalDurationSeconds,
    int? RpeRating,
    string? Notes,
    IReadOnlyList<ActiveExerciseSetInput>? FinalSets = null);

public sealed record CancelWorkoutSessionCommand(
    string IdempotencyKey,
    Guid SessionId);

public interface IWorkoutActiveSessionStore
{
    Task<ActiveWorkoutSessionSnapshot?> ResumeTimingAsync(Guid userId, ResumeWorkoutTimingCommand command, CancellationToken cancellationToken);
    Task<ActiveWorkoutSessionSnapshot?> CheckpointTimingAsync(Guid userId, CheckpointWorkoutTimingCommand command, CancellationToken cancellationToken);
    Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(Guid userId, int workoutDayId, CancellationToken cancellationToken);
    Task<ActiveWorkoutSessionSnapshot?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ActiveWorkoutSessionSnapshot> StartSessionAsync(
        Guid userId,
        StartWorkoutSessionCommand command,
        CancellationToken cancellationToken);

    Task<ActiveWorkoutSessionSnapshot?> UpsertSetAsync(
        Guid userId,
        UpsertWorkoutSessionSetCommand command,
        CancellationToken cancellationToken);

    Task<ActiveWorkoutSessionSnapshot?> FinishSessionAsync(
        Guid userId,
        FinishWorkoutSessionCommand command,
        CancellationToken cancellationToken);

    Task<ActiveWorkoutSessionSnapshot?> CancelSessionAsync(
        Guid userId,
        CancelWorkoutSessionCommand command,
        CancellationToken cancellationToken);
}
