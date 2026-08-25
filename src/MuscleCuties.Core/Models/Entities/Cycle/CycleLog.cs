using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Models.Entities.Cycle;

public class CycleLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int CycleLength { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<SymptomLog> SymptomLogs { get; set; } = new List<SymptomLog>();
    public ICollection<CyclePhaseLog> PhaseLogs { get; set; } = new List<CyclePhaseLog>();
}
