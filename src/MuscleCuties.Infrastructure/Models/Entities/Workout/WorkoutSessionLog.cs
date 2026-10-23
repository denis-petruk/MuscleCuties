using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Models.Entities.Workout;

public class WorkoutSessionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string WorkoutName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int TotalDurationSeconds { get; set; }
    public double TotalVolumeKg { get; set; }
    public string? Notes { get; set; }
    public int? RpeRating { get; set; } // 1-10
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<ExerciseSetLog> SetLogs { get; set; } = new Collection<ExerciseSetLog>();
}
