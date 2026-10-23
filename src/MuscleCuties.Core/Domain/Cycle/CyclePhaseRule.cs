using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Cycle;

public readonly record struct CyclePhaseObservation(CyclePhase Phase, DateTime LoggedAt);

public static class CyclePhaseRule
{
    public static int NormalizeCycleLength(int cycleLength)
    {
        return Math.Clamp(cycleLength > 0 ? cycleLength : CycleLengthLimits.Default,
            CycleLengthLimits.Minimum, CycleLengthLimits.Maximum);
    }

    public static CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        if (cycleDay <= 0)
            return CyclePhase.Unknown;
        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        if (cycleDay <= 5) return CyclePhase.Menstrual;

        var ovulationDay = normalizedCycleLength - 14;
        if (cycleDay <= ovulationDay - 2) return CyclePhase.Follicular;
        if (cycleDay <= ovulationDay + 2) return CyclePhase.Ovulatory;
        return CyclePhase.Luteal;
    }

    public static int GetPhaseAnchorDay(CyclePhase phase, int cycleLength)
    {
        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        var ovulationDay = Math.Max(6, normalizedCycleLength - 14);
        return phase switch
        {
            CyclePhase.Menstrual => 1,
            CyclePhase.Follicular => Math.Clamp(8, 6, Math.Max(6, ovulationDay - 2)),
            CyclePhase.Ovulatory => ovulationDay,
            CyclePhase.Luteal => Math.Min(normalizedCycleLength, Math.Max(7, ovulationDay + 4)),
            _ => 1
        };
    }

    public static CyclePhase GetNextPhase(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => CyclePhase.Follicular,
            CyclePhase.Follicular => CyclePhase.Ovulatory,
            CyclePhase.Ovulatory => CyclePhase.Luteal,
            CyclePhase.Luteal => CyclePhase.Menstrual,
            _ => CyclePhase.Unknown
        };
    }

    public static CyclePhase ProjectPhaseFromLog(
        CyclePhaseObservation observation, DateTime date, int cycleLength)
    {
        if (observation.Phase is CyclePhase.Unknown ||
            !Enum.IsDefined(observation.Phase) ||
            observation.LoggedAt == default ||
            date.Date < observation.LoggedAt.Date)
            return CyclePhase.Unknown;

        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        var anchorDay = GetPhaseAnchorDay(observation.Phase, normalizedCycleLength);
        var daysSinceShift = (date.Date - observation.LoggedAt.Date).Days;
        var projectedCycleDay = (anchorDay - 1 + daysSinceShift) % normalizedCycleLength + 1;
        return CalculatePhase(projectedCycleDay, normalizedCycleLength);
    }
}
