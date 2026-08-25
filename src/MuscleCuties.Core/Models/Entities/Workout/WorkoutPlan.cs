using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Models.Entities.Workout;

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
