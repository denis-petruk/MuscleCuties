using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public static class CyclePhaseRules
{
    public const int DefaultCycleLength = CycleLengthLimits.Default;
    public const int MinimumCycleLength = CycleLengthLimits.Minimum;
    public const int MaximumCycleLength = CycleLengthLimits.Maximum;

    public static int NormalizeCycleLength(int cycleLength)
    {
        return CyclePhaseRule.NormalizeCycleLength(cycleLength);
    }

    public static CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        return CyclePhaseRule.CalculatePhase(cycleDay, cycleLength);
    }

    public static int GetPhaseAnchorDay(CyclePhase phase, int cycleLength)
    {
        return CyclePhaseRule.GetPhaseAnchorDay(phase, cycleLength);
    }

    public static CyclePhase GetNextPhase(CyclePhase phase)
    {
        return CyclePhaseRule.GetNextPhase(phase);
    }

    public static CyclePhase ProjectPhaseFromLog(CyclePhaseLogProjection phaseLog, DateTime date, int cycleLength)
    {
        return CyclePhaseRule.ProjectPhaseFromLog(
            new CyclePhaseObservation(phaseLog.Phase, phaseLog.LoggedAt), date, cycleLength);
    }
}

public readonly record struct CyclePhaseLogProjection(CyclePhase Phase, DateTime LoggedAt);
