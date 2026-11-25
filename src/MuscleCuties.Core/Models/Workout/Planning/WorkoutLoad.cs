namespace MuscleCuties.Core.Models.Workout.Planning;

/// <summary>What kind of load yesterday carried, which the readiness rule reads.</summary>
public enum WorkoutLoadKind
{
    Unavailable = 0,
    NoRecordedWorkout = 1,
    ReportedRest = 2,
    AggregateOnly = 3,
    RecordedSets = 4
}

/// <summary>Yesterday's training load, or the reason it is unknown.</summary>
public sealed record WorkoutLoad(
    WorkoutLoadKind Kind,
    double? LoadScalar = null,
    int SetsCompleted = 0,
    double TotalVolumeKg = 0,
    int TotalDurationSeconds = 0)
{
    /// <summary>A load that could not be determined, which scores neutrally rather than as rest.</summary>
    public static WorkoutLoad Unavailable => new(WorkoutLoadKind.Unavailable);
    /// <summary>A day with no session, which counts as rest.</summary>
    public static WorkoutLoad NoRecordedWorkout => new(WorkoutLoadKind.NoRecordedWorkout);
    /// <summary>A day she reported as rest, which differs from a day without records.</summary>
    public static WorkoutLoad ReportedRest => new(WorkoutLoadKind.ReportedRest, 0.0);
}
