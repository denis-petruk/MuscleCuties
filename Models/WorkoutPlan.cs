using System.ComponentModel.DataAnnotations;
using MuscleCuties.Models.Enums;

namespace MuscleCuties.Models;

public class WorkoutPlan
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required] public string Name { get; set; } = null!;
    public int DaysPerWeek { get; set; }
    public CyclePhase Phase { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<WorkoutDay> WorkoutDays { get; set; } = new List<WorkoutDay>();
}