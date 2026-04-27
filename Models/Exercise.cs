using System.ComponentModel.DataAnnotations;
using MuscleCuties.Models.Enums;

namespace MuscleCuties.Models;

public class Exercise
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = null!;
    [Required] public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public MuscleGroup PrimaryMuscle { get; set; }
    public string? SecondaryMuscles { get; set; }

    // Comma-separated joint areas this exercise stresses.
    // Valid values: Shoulder, Knee, LowerBack, Wrist, Hip, Elbow
    // Empty string = no joint constraints.
    public string JointAreas { get; set; } = string.Empty;

    public ICollection<WorkoutExercise> WorkoutExercises { get; set; } = new List<WorkoutExercise>();
}