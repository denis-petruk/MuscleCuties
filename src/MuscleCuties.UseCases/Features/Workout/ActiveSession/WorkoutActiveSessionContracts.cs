using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.ActiveSession;

public sealed class WorkoutHiitHistoryConfirmationRequiredException : Exception
{
    public const string ErrorCode = "WorkoutHiitHistoryConfirmationRequired";

    public WorkoutHiitHistoryConfirmationRequiredException()
        : base("Confirm or update your HIIT history before starting this activity.") { }
}

public sealed class WorkoutActivityNoLongerSafeException : Exception
{
    public WorkoutActivityNoLongerSafeException() : base("The activity is no longer safe to continue.") { }
}

public sealed class WorkoutTimingIntervalExpiredException : Exception
{
    public WorkoutTimingIntervalExpiredException() : base("The active interval expired; resume to continue.") { }
}

/// <summary>
/// Reasons the server refuses to start or resume a planned activity. Each code maps to one guided
/// client message so a refusal is never reported as a generic failure.
/// </summary>
public static class WorkoutSessionStartBlockCode
{
    public const string PlannedWorkoutUnavailable = "WorkoutStartPlannedUnavailable";
    public const string TimeZoneNotEstablished = "WorkoutStartTimeZoneMissing";
    public const string NotScheduledToday = "WorkoutStartNotToday";
    public const string ActiveSessionOrPreviewOpen = "WorkoutStartSessionOrPreviewOpen";
    public const string PrescriptionChanged = "WorkoutStartPrescriptionChanged";
    public const string SafetyReviewRequired = "WorkoutStartSafetyReview";
    public const string ActiveWorkoutInProgress = "WorkoutStartActiveWorkout";
    public const string SessionNotActive = "WorkoutSessionNotActive";

    public static bool IsKnown(string? code) => code is not null && AllCodes.Contains(code);

    private static readonly IReadOnlySet<string> AllCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        PlannedWorkoutUnavailable,
        TimeZoneNotEstablished,
        NotScheduledToday,
        ActiveSessionOrPreviewOpen,
        PrescriptionChanged,
        SafetyReviewRequired,
        ActiveWorkoutInProgress,
        SessionNotActive
    };
}

public sealed class WorkoutSessionStartBlockedException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
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
    public decimal? DistanceKm { get; init; }
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
    int? PlannedWorkoutDayId = null,
    WorkoutActivityDto? UnplannedActivity = null);

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
    IReadOnlyList<ActiveExerciseSetInput>? FinalSets = null,
    decimal? DistanceKm = null);

public sealed record CancelWorkoutSessionCommand(
    string IdempotencyKey,
    Guid SessionId);

public sealed record PlannedWorkoutSessionHistoryEntry(Guid SessionId, DateTime StartedAtUtc, DateTime? CompletedAtUtc);

public interface IWorkoutActiveSessionStore
{
    Task<IReadOnlyList<PlannedWorkoutSessionHistoryEntry>> GetPlannedSessionHistoryAsync(Guid userId, int workoutDayId, CancellationToken cancellationToken);
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
