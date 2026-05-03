using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class Exercise
{
    public int Id { get; set; }
    [Required] public string Code { get; set; } = null!;
    [Required] public string Name { get; set; } = null!;
    [Required] public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public MuscleGroup PrimaryMuscle { get; set; }
    public string? SecondaryMuscles { get; set; }
    public string JointAreas { get; set; } = string.Empty;
    public bool IsInjuryFriendly { get; set; }

    public ICollection<WorkoutDayExercise> WorkoutExercises { get; set; } = new List<WorkoutDayExercise>();
}
