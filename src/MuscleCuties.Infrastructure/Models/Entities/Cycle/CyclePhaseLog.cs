using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.Entities.Cycle;

/// <summary>One day's phase record: the phase, the flow when there was any, and the note she added.</summary>
public class CyclePhaseLog
{
    /// <summary>The stored identity.</summary>
    public int Id { get; set; }
    /// <summary>The account the log belongs to.</summary>
    public int UserId { get; set; }
    /// <summary>The cycle it belongs to, or null for a log made before any cycle was recorded.</summary>
    public int? CycleLogId { get; set; }
    /// <summary>The phase recorded for the day.</summary>
    public CyclePhase Phase { get; set; }
    /// <summary>The flow, or null when she did not record any.</summary>
    public FlowIntensity? FlowIntensity { get; set; }
    /// <summary>The day the log is about.</summary>
    public DateTime LoggedAt { get; set; }
    /// <summary>Her own note for the day.</summary>
    public string? Note { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The navigation to the account.</summary>
    public User? User { get; set; }
    /// <summary>The navigation to the owning cycle.</summary>
    public CycleLog? CycleLog { get; set; }
}
