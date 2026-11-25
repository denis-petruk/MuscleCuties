using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.ActiveSession;

/// <summary>The client must confirm that earlier HIIT work is being superseded.</summary>
public sealed class WorkoutHiitHistoryConfirmationRequiredException : Exception
{
    public const string ErrorCode = "WorkoutHiitHistoryConfirmationRequired";

    /// <summary>The client must confirm that earlier HIIT work is being replaced before the session proceeds.</summary>
    public WorkoutHiitHistoryConfirmationRequiredException()
        : base("Confirm or update your HIIT history before starting this activity.") { }
}

/// <summary>The activity was blocked by an injury logged after it was planned.</summary>
public sealed class WorkoutActivityNoLongerSafeException : Exception
{
    /// <summary>Reports that the activity became unsafe to continue.</summary>
    public WorkoutActivityNoLongerSafeException() : base("The activity is no longer safe to continue.") { }
}

/// <summary>The timing interval ran out before the command arrived, so the client must resync.</summary>
public sealed class WorkoutTimingIntervalExpiredException : Exception
{
    /// <summary>Reports that the timed interval expired and must be resumed.</summary>
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

    /// <summary>True when the code names a known timing reason.</summary>
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

/// <summary>A session cannot start, with the reason the client must show.</summary>
public sealed class WorkoutSessionStartBlockedException(string code) : InvalidOperationException(code)
{
    /// <summary>Stable code the client uses to recognize the reason.</summary>
    public string Code { get; } = code;
}

/// <summary>One set being logged, with its weight, reps and rest.</summary>
public sealed record ActiveExerciseSetInput(
    Guid SetId,
    string? ExerciseId,
    string ExerciseName,
    int SetNumber,
    decimal WeightKg,
    int RepsCompleted,
    decimal? Rpe,
    bool IsWarmup,
    int RestTimeSeconds,
    int? LeftSideReps = null,
    int? RightSideReps = null);

/// <summary>One logged set as the session screen shows it.</summary>
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
    DateTime UpdatedAtUtc,
    int? LeftSideReps = null,
    int? RightSideReps = null);

/// <summary>The session in progress, with its exercises and elapsed time.</summary>
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
    /// <summary>Active seconds already banked at the last timing checkpoint, so a restart does not lose them.</summary>
    public int ConfirmedActiveSeconds { get; init; }
    /// <summary>The interval currently being timed, or null when no interval is running.</summary>
    public Guid? TimingIntervalId { get; init; }
    /// <summary>Seconds confirmed for that interval, which the next checkpoint builds on.</summary>
    public int TimingIntervalConfirmedSeconds { get; init; }
    /// <summary>Distance for cardio activities, absent for strength work that has no distance.</summary>
    public decimal? DistanceKm { get; init; }
}

/// <summary>A request to resume the session clock.</summary>
public sealed record ResumeWorkoutTimingCommand(string IdempotencyKey, Guid SessionId);
/// <summary>A clock checkpoint, idempotent on its key.</summary>
public sealed record CheckpointWorkoutTimingCommand(string IdempotencyKey, Guid SessionId, Guid IntervalId,
    int MeasuredSeconds, bool Pause);

/// <summary>A request to start a session, carrying the plan it starts from.</summary>
public sealed record StartWorkoutSessionCommand(
    string IdempotencyKey,
    Guid SessionId,
    string WorkoutName,
    DateTime StartedAtUtc,
    string? Notes = null,
    int? PlannedWorkoutDayId = null,
    WorkoutActivityDto? UnplannedActivity = null);

/// <summary>A set to log or change, idempotent on its key.</summary>
public sealed record UpsertWorkoutSessionSetCommand(
    string IdempotencyKey,
    Guid SessionId,
    ActiveExerciseSetInput Set);

/// <summary>A request to finish, carrying the summary she confirmed.</summary>
public sealed record FinishWorkoutSessionCommand(
    string IdempotencyKey,
    Guid SessionId,
    DateTime CompletedAtUtc,
    int TotalDurationSeconds,
    int? RpeRating,
    string? Notes,
    IReadOnlyList<ActiveExerciseSetInput>? FinalSets = null,
    decimal? DistanceKm = null);

/// <summary>A request to abandon the session without recording it.</summary>
public sealed record CancelWorkoutSessionCommand(
    string IdempotencyKey,
    Guid SessionId);

/// <summary>A planned session that was already performed, which the planner must not schedule again.</summary>
public sealed record PlannedWorkoutSessionHistoryEntry(Guid SessionId, DateTime StartedAtUtc, DateTime? CompletedAtUtc);

/// <summary>The active-session persistence, including its command receipts.</summary>
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
