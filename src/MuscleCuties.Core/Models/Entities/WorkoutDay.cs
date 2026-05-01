using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Core.Models.Entities;

public class WorkoutDay
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    public int DayOfWeek { get; set; } 
    [Required] public string Name { get; set; } = null!;

    public WorkoutPlan? WorkoutPlan { get; set; }
    public ICollection<WorkoutDayExercise> WorkoutDayExercises { get; set; } = new List<WorkoutDayExercise>();
}