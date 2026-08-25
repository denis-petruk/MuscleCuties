using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public class CyclePredictionPlanner : ICyclePredictionPlanner
{
    private const int HistoryCyclesToUse = 6;

    private readonly ICyclePhaseCalculator _phaseCalculator;

    public CyclePredictionPlanner(ICyclePhaseCalculator phaseCalculator)
    {
        _phaseCalculator = phaseCalculator;
    }

    public CyclePrediction CreatePrediction(
        CycleLog? latestCycle,
        IReadOnlyCollection<CycleLog> history,
        UserProfile? profile,
        DateTime today)
    {
        var (cycleLength, source) = PredictCycleLength(history, profile?.CycleLength);

        if (latestCycle is null)
        {
            var profilePhase = profile?.CurrentCyclePhase;
            return new CyclePrediction
            {
                HasActiveCycle = false,
                CurrentDay = 0,
                PredictedCycleLength = cycleLength,
                CurrentPhase = profilePhase ?? CyclePhase.Follicular,
                DaysUntilPeriod = 0,
                PredictionSource = profilePhase is null ? source : "profile phase"
            };
        }

        var startDate = latestCycle.StartDate.Date;
        var currentDay = Math.Max(1, (today.Date - startDate).Days + 1);
        var nextPeriodDate = startDate.AddDays(cycleLength);
        var daysUntilPeriod = Math.Max(0, (nextPeriodDate - today.Date).Days);
        var ovulationDate = startDate.AddDays(Math.Max(0, cycleLength - 14));

        return new CyclePrediction
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = startDate,
            CurrentDay = currentDay,
            PredictedCycleLength = cycleLength,
            CurrentPhase = _phaseCalculator.CalculatePhase(currentDay, cycleLength),
            PredictedNextPeriodDate = nextPeriodDate,
            DaysUntilPeriod = daysUntilPeriod,
            PredictedOvulationDate = ovulationDate,
            FertileWindowStartDate = ovulationDate.AddDays(-5),
            FertileWindowEndDate = ovulationDate.AddDays(1),
            PredictionSource = source
        };
    }

    private static (int CycleLength, string Source) PredictCycleLength(
        IReadOnlyCollection<CycleLog> history,
        int? profileCycleLength)
    {
        var measuredLengths = history
            .Where(cycle => IsUsableCycleLength(cycle.CycleLength))
            .OrderByDescending(cycle => cycle.StartDate)
            .Take(HistoryCyclesToUse)
            .Select(cycle => cycle.CycleLength)
            .ToList();

        if (measuredLengths.Count > 0)
            return (ClampCycleLength((int)Math.Round(measuredLengths.Average())), "recent cycles");

        var startDateLengths = CalculateStartDateLengths(history)
            .Where(IsUsableCycleLength)
            .TakeLast(HistoryCyclesToUse)
            .ToList();

        if (startDateLengths.Count > 0)
            return (ClampCycleLength((int)Math.Round(startDateLengths.Average())), "cycle start history");

        if (profileCycleLength is not null && IsUsableCycleLength(profileCycleLength.Value))
            return (profileCycleLength.Value, "profile");

        return (CyclePhaseRules.DefaultCycleLength, "default");
    }

    private static bool IsUsableCycleLength(int cycleLength) =>
        cycleLength is >= CyclePhaseRules.MinimumCycleLength and <= CyclePhaseRules.MaximumCycleLength;

    private static int ClampCycleLength(int cycleLength) =>
        CyclePhaseRules.NormalizeCycleLength(cycleLength);

    private static IEnumerable<int> CalculateStartDateLengths(IEnumerable<CycleLog> history)
    {
        var orderedCycles = history
            .OrderBy(cycle => cycle.StartDate)
            .ToList();

        for (var i = 1; i < orderedCycles.Count; i++)
            yield return (orderedCycles[i].StartDate.Date - orderedCycles[i - 1].StartDate.Date).Days;
    }
}
