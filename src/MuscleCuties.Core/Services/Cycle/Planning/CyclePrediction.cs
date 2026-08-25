using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public sealed class CyclePrediction
{
    public bool HasActiveCycle { get; init; }
    public DateTime? CurrentCycleStartDate { get; init; }
    public int CurrentDay { get; init; }
    public int PredictedCycleLength { get; init; }
    public CyclePhase CurrentPhase { get; init; }
    public DateTime? PredictedNextPeriodDate { get; init; }
    public int DaysUntilPeriod { get; init; }
    public DateTime? PredictedOvulationDate { get; init; }
    public DateTime? FertileWindowStartDate { get; init; }
    public DateTime? FertileWindowEndDate { get; init; }
    public string PredictionSource { get; init; } = string.Empty;
    public bool IsPeriodDue => HasActiveCycle && DaysUntilPeriod == 0;
}
