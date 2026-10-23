namespace MuscleCuties.Core.Models.Entities.Workout;

public class WorkoutExerciseLog
{
    public int Id { get; set; }
    public int WorkoutLogId { get; set; }
    public int WorkoutDayExerciseId { get; set; }
    public int ExerciseId { get; set; }
    public int CompletedSets { get; set; }
    public int CompletedReps { get; set; }
    public float? WeightKg { get; set; }
    public int? CompletedDurationSeconds { get; set; }
    public float? DistanceKm { get; set; }
    public int? AverageHeartRateBpm { get; set; }
    public int? PaceSecondsPerKm { get; set; }
    public int? PowerWatts { get; set; }
    public int? CadenceRpm { get; set; }
    public int? EffortRating { get; set; }
    public DateTime CreatedAt { get; set; }

    public WorkoutLog? WorkoutLog { get; set; }
    public WorkoutDayExercise? WorkoutDayExercise { get; set; }
    public Exercise? Exercise { get; set; }
}
