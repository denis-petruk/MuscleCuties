using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.Entities.Workout;

public class Exercise
{
    public int Id { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = null!;
    [Required] public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? TechniqueNotes { get; set; }
    public MuscleGroup PrimaryMuscle { get; set; }
    public string? SecondaryMuscles { get; set; }
    // Comma-separated joint areas this exercise stresses (Shoulder, Knee, LowerBack, Wrist, Hip, Elbow).
    // Empty string means no joint constraints.
    public string JointAreas { get; set; } = string.Empty;
    public bool IsInjuryFriendly { get; set; }

    public ICollection<WorkoutDayExercise> WorkoutExercises { get; set; } = new List<WorkoutDayExercise>();
}
