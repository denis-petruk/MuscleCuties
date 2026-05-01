using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class WorkoutPlan
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required] public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public CyclePhase? CyclePhaseTarget { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<WorkoutDay> WorkoutDays { get; set; } = new List<WorkoutDay>();
}