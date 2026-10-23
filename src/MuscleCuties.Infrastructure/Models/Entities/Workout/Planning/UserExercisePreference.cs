namespace MuscleCuties.Core.Models.Entities.Workout.Planning;

public class UserExercisePreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int OriginalExerciseId { get; set; }
    public int PreferredExerciseId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
