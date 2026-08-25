using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.Entities.Cycle;

public class SymptomLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CycleLogId { get; set; }
    public DateTime Date { get; set; }
    public SymptomType SymptomType { get; set; }
    public int Severity { get; set; } // 1–5
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public CycleLog? CycleLog { get; set; }
}
