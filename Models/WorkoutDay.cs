using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Models;

public class WorkoutDay
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    public int DayNumber { get; set; }
    [Required] public string Name { get; set; } = null!;

    public WorkoutPlan? WorkoutPlan { get; set; }
    public ICollection<WorkoutExercise> WorkoutExercises { get; set; } = new List<WorkoutExercise>();
}