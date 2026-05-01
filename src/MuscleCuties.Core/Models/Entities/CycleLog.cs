namespace MuscleCuties.Core.Models.Entities;

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
}