using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.Entities.Cycle;

/// <summary>One symptom on one day of a cycle, with the severity she reported.</summary>
public class SymptomLog
{
    /// <summary>Identity of the stored symptom log.</summary>
    public int Id { get; set; }
    /// <summary>The account the log belongs to.</summary>
    public int UserId { get; set; }
    /// <summary>The cycle it was logged in; a symptom always belongs to one.</summary>
    public int CycleLogId { get; set; }
    /// <summary>The day the symptom was felt.</summary>
    public DateTime Date { get; set; }
    /// <summary>Which symptom it was.</summary>
    public SymptomType SymptomType { get; set; }
    /// <summary>How strong it was, on the app's fixed severity scale.</summary>
    public int Severity { get; set; }
    /// <summary>Her own note about the symptom.</summary>
    public string? Notes { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The navigation to the owning cycle.</summary>
    public CycleLog? CycleLog { get; set; }
}
