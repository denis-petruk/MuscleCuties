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

    public ICollection<WorkoutExercise> WorkoutExercises { get; set; } = new List<WorkoutExercise>();
}