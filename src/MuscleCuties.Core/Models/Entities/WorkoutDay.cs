using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class WorkoutDay
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    public int DayOfWeek { get; set; }
    [Required] public string Name { get; set; } = null!;
    public WorkoutType WorkoutType { get; set; }
    public int DurationMinutes { get; set; }

    public WorkoutPlan? WorkoutPlan { get; set; }
    public ICollection<WorkoutDayExercise> WorkoutDayExercises { get; set; } = new List<WorkoutDayExercise>();
}
