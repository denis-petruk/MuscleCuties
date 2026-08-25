using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public static class CyclePhaseRules
{
    public const int DefaultCycleLength = 28;
    public const int MinimumCycleLength = 18;
    public const int MaximumCycleLength = 60;

    public static int NormalizeCycleLength(int cycleLength) =>
        Math.Clamp(cycleLength > 0 ? cycleLength : DefaultCycleLength, MinimumCycleLength, MaximumCycleLength);

    public static CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        var normalizedDay = Math.Max(1, cycleDay);

        if (normalizedDay <= 5) return CyclePhase.Menstrual;

        var ovulationDay = normalizedCycleLength - 14;
        if (normalizedDay <= ovulationDay - 2) return CyclePhase.Follicular;
        if (normalizedDay <= ovulationDay + 2) return CyclePhase.Ovulatory;

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

    public static CyclePhase GetNextPhase(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => CyclePhase.Follicular,
        CyclePhase.Follicular => CyclePhase.Ovulatory,
        CyclePhase.Ovulatory => CyclePhase.Luteal,
        CyclePhase.Luteal => CyclePhase.Menstrual,
        _ => CyclePhase.Menstrual
    };

    public static CyclePhase ProjectPhaseFromLog(CyclePhaseLogProjection phaseLog, DateTime date, int cycleLength)
    {
        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        var anchorDay = GetPhaseAnchorDay(phaseLog.Phase, normalizedCycleLength);
        var daysSinceShift = Math.Max(0, (date.Date - phaseLog.LoggedAt.Date).Days);
        var projectedCycleDay = ((anchorDay - 1 + daysSinceShift) % normalizedCycleLength) + 1;

        return CalculatePhase(projectedCycleDay, normalizedCycleLength);
    }
}

public readonly record struct CyclePhaseLogProjection(CyclePhase Phase, DateTime LoggedAt);
