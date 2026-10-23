namespace MuscleCuties.Core.Models.Workout.Planning;

public enum WorkoutLoadKind
{
    Unavailable = 0,
    NoRecordedWorkout = 1,
    ReportedRest = 2,
    AggregateOnly = 3,
    RecordedSets = 4
}

public sealed record WorkoutLoad(
    WorkoutLoadKind Kind,
    double? LoadScalar = null,
    int SetsCompleted = 0,
    double TotalVolumeKg = 0,
    int TotalDurationSeconds = 0)
{
    public static WorkoutLoad Unavailable => new(WorkoutLoadKind.Unavailable);
    public static WorkoutLoad NoRecordedWorkout => new(WorkoutLoadKind.NoRecordedWorkout);
    public static WorkoutLoad ReportedRest => new(WorkoutLoadKind.ReportedRest, 0.0);
}
