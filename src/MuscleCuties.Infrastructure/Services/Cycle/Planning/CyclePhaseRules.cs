using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

/// <summary>The one set of phase boundaries and transitions, shared by the calculator, the planner and the projection.</summary>
public static class CyclePhaseRules
{
    public const int DefaultCycleLength = CycleLengthLimits.Default;
    public const int MinimumCycleLength = CycleLengthLimits.Minimum;
    public const int MaximumCycleLength = CycleLengthLimits.Maximum;

    /// <summary>A cycle length clamped to the range the rules are defined for, so an extreme value cannot produce a phase no</summary>
    /// day belongs to.</summary>
    public static int NormalizeCycleLength(int cycleLength)
    {
        return CyclePhaseRule.NormalizeCycleLength(cycleLength);
    }

    /// <summary>The phase for a day of a cycle of the given length.</summary>
    public static CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        return CyclePhaseRule.CalculatePhase(cycleDay, cycleLength);
    }

    /// <summary>The day a phase starts on, which is what a projection counts from.</summary>
    public static int GetPhaseAnchorDay(CyclePhase phase, int cycleLength)
    {
        return CyclePhaseRule.GetPhaseAnchorDay(phase, cycleLength);
    }

    /// <summary>The phase that follows one, wrapping from the last back to the first.</summary>
    public static CyclePhase GetNextPhase(CyclePhase phase)
    {
        return CyclePhaseRule.GetNextPhase(phase);
    }

    /// <summary>The phase on a later day, projected from a logged phase and the days since it.</summary>
    public static CyclePhase ProjectPhaseFromLog(CyclePhaseLogProjection phaseLog, DateTime date, int cycleLength)
    {
        return CyclePhaseRule.ProjectPhaseFromLog(
            new CyclePhaseObservation(phaseLog.Phase, phaseLog.LoggedAt), date, cycleLength);
    }
}

/// <summary>A logged phase with its day, which is what a projection needs to place a later day.</summary>
public readonly record struct CyclePhaseLogProjection(CyclePhase Phase, DateTime LoggedAt);
