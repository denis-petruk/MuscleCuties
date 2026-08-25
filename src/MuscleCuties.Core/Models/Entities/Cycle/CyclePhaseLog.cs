using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.Entities.Cycle;

public class CyclePhaseLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? CycleLogId { get; set; }
    public CyclePhase Phase { get; set; }
    public DateTime LoggedAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public CycleLog? CycleLog { get; set; }
}
