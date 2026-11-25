namespace MuscleCuties.Core.Domain.Cycle;

/// <summary>Conservative cycle-length bounds used for calendar color projection.</summary>
public static class CycleCalendarProjectionRule
{
    public const int MinimumCalendarCycleLength = 21;
    public const int MaximumCalendarCycleLength = 45;

    /// <summary>Clamps a user-entered cycle length into the range the calendar can project.</summary>
    public static int NormalizeCycleLength(int cycleLength) =>
        Math.Clamp(cycleLength, MinimumCalendarCycleLength, MaximumCalendarCycleLength);
}
