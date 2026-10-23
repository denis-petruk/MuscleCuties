using MuscleCuties.Core.Domain.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public sealed record CycleObservation(DateTime StartDate, DateTime? EndDate, int CycleLength);

// Compatibility entry point for existing callers; all prediction math is in Domain.
public sealed class CyclePredictionRule
{
    public CyclePredictionRule(ICyclePhaseCalculator phaseCalculator)
    {
        ArgumentNullException.ThrowIfNull(phaseCalculator);
    }

    public CyclePrediction CreatePrediction(
        CycleObservation? latestCycle,
        IReadOnlyCollection<CycleObservation>? history,
        int? profileCycleLength,
        DateTime today)
    {
        var latest = latestCycle is null ? null : Map(latestCycle);
        var observations = history?.Where(cycle => cycle is not null).Select(Map).ToArray();
        return Map(CyclePredictionDomainRule.CreatePrediction(latest, observations, profileCycleLength, today));
    }

    private static Domain.Cycle.CycleObservation Map(CycleObservation cycle)
    {
        return new Domain.Cycle.CycleObservation(cycle.StartDate, cycle.EndDate, cycle.CycleLength);
    }

    private static CyclePrediction Map(CyclePredictionEstimate estimate)
    {
        return new CyclePrediction
        {
            HasActiveCycle = estimate.HasActiveCycle,
            CurrentCycleStartDate = estimate.CurrentCycleStartDate,
            CurrentDay = estimate.CurrentDay,
            PredictedCycleLength = estimate.PredictedCycleLength,
            CurrentPhase = estimate.CurrentPhase,
            PredictedNextPeriodDate = estimate.PredictedNextPeriodDate,
            DaysUntilPeriod = estimate.DaysUntilPeriod,
            PredictedOvulationDate = estimate.PredictedOvulationDate,
            FertileWindowStartDate = estimate.FertileWindowStartDate,
            FertileWindowEndDate = estimate.FertileWindowEndDate,
            PredictionSource = estimate.PredictionSource
        };
    }
}
