using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Models.Entities.Cycle;

/// <summary>One recorded cycle: its start, its optional end and the length it turned out to be.</summary>
public class CycleLog
{
    /// <summary>The stored identity.</summary>
    public int Id { get; set; }
    /// <summary>The account the cycle belongs to.</summary>
    public int UserId { get; set; }
    /// <summary>The first day of the cycle.</summary>
    public DateTime StartDate { get; set; }
    /// <summary>The last day, or null while the cycle is still running.</summary>
    public DateTime? EndDate { get; set; }
    /// <summary>How many days the cycle lasted, which is what the next prediction uses.</summary>
    public int CycleLength { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The navigation to the account.</summary>
    public User? User { get; set; }
    /// <summary>The symptoms logged during this cycle.</summary>
    public ICollection<SymptomLog> SymptomLogs { get; set; } = new List<SymptomLog>();
    /// <summary>The phase logs that belong to this cycle.</summary>
    public ICollection<CyclePhaseLog> PhaseLogs { get; set; } = new List<CyclePhaseLog>();
}
