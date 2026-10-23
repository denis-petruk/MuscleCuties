using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Cycle.Planning;

// Maps legacy EF records into detached observations until the cycle read port is migrated.
public sealed class CyclePredictionPlanner(ICyclePhaseCalculator phaseCalculator) : ICyclePredictionPlanner
{
    private readonly CyclePredictionRule _rule = new(phaseCalculator);

    public CyclePrediction CreatePrediction(
        CycleLog? latestCycle,
        IReadOnlyCollection<CycleLog> history,
        UserProfile? profile,
        DateTime today)
    {
        var latest = latestCycle is null ? null : Map(latestCycle);
        var observations = history?.Where(cycle => cycle is not null)
            .Select(Map).ToArray();
        return _rule.CreatePrediction(latest, observations, profile?.CycleLength, today);
    }

    private static CycleObservation Map(CycleLog cycle)
    {
        return new CycleObservation(cycle.StartDate, cycle.EndDate, cycle.CycleLength);
    }
}
