namespace MuscleCuties.Core.Models.Entities.Workout;

public class ExerciseSetLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkoutSessionLogId { get; set; }
    public string? ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public int SetNumber { get; set; }
    public double WeightKg { get; set; }
    public int RepsCompleted { get; set; }
    public double? Rpe { get; set; }
    public bool IsWarmup { get; set; }
    public int RestTimeSeconds { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkoutSessionLog? WorkoutSessionLog { get; set; }
}
