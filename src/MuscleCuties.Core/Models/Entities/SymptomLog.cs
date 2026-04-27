using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class SymptomLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CycleLogId { get; set; }
    public DateTime Date { get; set; }
    public CyclePhase Phase { get; set; }
    public int Pain { get; set; }
    public int Energy { get; set; }
    public int Sleep { get; set; }
    public int Bloating { get; set; }
    public int Appetite { get; set; }
    public int Stress { get; set; }
    public string? Notes { get; set; }

    public CycleLog? CycleLog { get; set; }
}
