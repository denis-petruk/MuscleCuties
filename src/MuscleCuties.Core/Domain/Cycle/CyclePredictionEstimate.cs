using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Cycle;

/// <summary>Takes the prediction's inputs, so the derived values stay one consistent set.</summary>
public sealed class CyclePredictionEstimate
{
    /// <summary>Whether a current cycle exists to predict from.</summary>
    public bool HasActiveCycle { get; init; }
    /// <summary>The last recorded start, absent when no cycle is active.</summary>
    public DateTime? CurrentCycleStartDate { get; init; }
    /// <summary>The day within the current cycle, counted from its start.</summary>
    public int CurrentDay { get; init; }
    /// <summary>The length the prediction expects, clamped to the supported range.</summary>
    public int PredictedCycleLength { get; init; }
    /// <summary>The phase today falls in, or unknown when no cycle is active.</summary>
    public CyclePhase CurrentPhase { get; init; }
    /// <summary>The expected start of the next cycle.</summary>
    public DateTime? PredictedNextPeriodDate { get; init; }
    /// <summary>Days until that date, floored at zero once it has passed.</summary>
    public int DaysUntilPeriod { get; init; }
    /// <summary>The predicted ovulation date, derived from the cycle length rather than measured.</summary>
    public DateTime? PredictedOvulationDate { get; init; }
    /// <summary>First day of the predicted fertile window, when one is estimated.</summary>
    public DateTime? FertileWindowStartDate { get; init; }
    /// <summary>Last day of the predicted fertile window, when one is estimated.</summary>
    public DateTime? FertileWindowEndDate { get; init; }
    /// <summary>Which input the predicted cycle length came from.</summary>
    public string PredictionSource { get; init; } = string.Empty;
    /// <summary>True when the estimate puts the next period on today.</summary>
    public bool IsPeriodDue => HasActiveCycle && DaysUntilPeriod == 0;
}
