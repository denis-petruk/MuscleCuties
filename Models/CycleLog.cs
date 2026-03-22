namespace MuscleCuties.Models;

public class CycleLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CycleStartDate { get; set; }
    public DateTime? CycleEndDate { get; set; }
    public int CycleLength { get; set; }
    public int PeriodLength { get; set; }

    public User? User { get; set; }
    public ICollection<SymptomLog> SymptomLogs { get; set; } = new List<SymptomLog>();
}