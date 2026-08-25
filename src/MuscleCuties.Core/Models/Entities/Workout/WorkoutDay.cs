using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.Entities.Workout;

public class WorkoutDay
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    public int DayOfWeek { get; set; }
    public WorkoutType WorkoutType { get; set; }
    [Required] public string Name { get; set; } = null!;

    public WorkoutPlan? WorkoutPlan { get; set; }
    public ICollection<WorkoutDayExercise> WorkoutDayExercises { get; set; } = new List<WorkoutDayExercise>();
}
