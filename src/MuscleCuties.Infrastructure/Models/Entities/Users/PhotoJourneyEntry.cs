using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.Entities.Users;

public class PhotoJourneyEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required] public string ImagePath { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public double? WeightKg { get; set; }
    public string Notes { get; set; } = string.Empty;
    public CyclePhase Phase { get; set; } = CyclePhase.Follicular;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
