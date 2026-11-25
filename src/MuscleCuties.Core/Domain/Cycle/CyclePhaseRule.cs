using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Cycle;

/// <summary>One observed cycle start with its length, the evidence a phase is derived from.</summary>
public readonly record struct CyclePhaseObservation(CyclePhase Phase, DateTime LoggedAt);

/// <summary>Derives phases from cycle lengths, clamping every length into the supported range first.</summary>
public static class CyclePhaseRule
{
    private const int NoCycleDay = 0;
    private const int FirstCycleDay = 1;
    private const int FirstProjectedOffset = 1;
    private const int LastMenstrualDay = 5;
    private const int DaysBeforeExpectedPeriodForOvulation = 14;
    private const int OvulationWindowHalfWidthDays = 2;
    private const int FirstPossibleOvulationDay = 6;
    private const int FollicularPhaseAnchorDay = 8;
    private const int LutealAnchorDaysAfterOvulation = 4;
    private const int MinimumLutealAnchorDay = 7;
    private const int NoDaysRemaining = 0;
    private const int DefaultCycleDay = 1;

    /// <summary>Clamps a length into the supported range, so an outlier cannot shift every later phase.</summary>
    public static int NormalizeCycleLength(int cycleLength)
    {
        return Math.Clamp(cycleLength > 0 ? cycleLength : CycleLengthLimits.Default,
            CycleLengthLimits.Minimum, CycleLengthLimits.Maximum);
    }

    /// <summary>The phase for a cycle day, using the standard split of the cycle length.</summary>
    public static CyclePhase CalculatePhase(int cycleDay, int cycleLength)
    {
        if (cycleDay <= NoCycleDay)
            return CyclePhase.Unknown;
        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        if (cycleDay <= LastMenstrualDay) return CyclePhase.Menstrual;

        var ovulationDay = normalizedCycleLength - DaysBeforeExpectedPeriodForOvulation;
        if (cycleDay <= ovulationDay - OvulationWindowHalfWidthDays) return CyclePhase.Follicular;
        if (cycleDay <= ovulationDay + OvulationWindowHalfWidthDays) return CyclePhase.Ovulatory;
        return CyclePhase.Luteal;
    }

    /// <summary>The day within the cycle, counted from the last start and wrapped at the length.</summary>
    public static int CalculateCycleDay(DateTime date, DateTime? cycleStartDate, int cycleLength)
    {
        if (cycleStartDate is null)
            return NoCycleDay;

        return ProjectCycleDay(DefaultCycleDay, cycleStartDate.Value, date, cycleLength);
    }

    /// <summary>The day within the cycle for a future date, which is what the countdown reads.</summary>
    public static int ProjectCycleDay(int cycleDay, DateTime cycleDayDate, DateTime date, int cycleLength)
    {
        if (cycleDay <= NoCycleDay)
            return NoCycleDay;

        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        var normalizedAnchorDay = Math.Clamp(cycleDay, FirstCycleDay, normalizedCycleLength);
        var daysFromAnchor = (date.Date - cycleDayDate.Date).Days;
        var projectedOffset = (normalizedAnchorDay - FirstCycleDay + daysFromAnchor) % normalizedCycleLength;
        return (projectedOffset + normalizedCycleLength) % normalizedCycleLength + FirstCycleDay;
    }

    /// <summary>Days until the phase changes, floored at zero once it has.</summary>
    public static int GetDaysRemainingInPhase(int cycleDay, int cycleLength, CyclePhase phase)
    {
        if (cycleDay <= NoCycleDay || phase is CyclePhase.Unknown || !Enum.IsDefined(phase))
            return NoDaysRemaining;

        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        var normalizedCycleDay = Math.Clamp(cycleDay, FirstCycleDay, normalizedCycleLength);
        var daysRemaining = NoDaysRemaining;
        for (var offset = FirstProjectedOffset; offset <= normalizedCycleLength; offset++)
        {
            var projectedDay = (normalizedCycleDay - FirstCycleDay + offset) % normalizedCycleLength + FirstCycleDay;
            if (CalculatePhase(projectedDay, normalizedCycleLength) != phase)
                break;

            daysRemaining++;
        }

        return daysRemaining;
    }

    /// <summary>The first day of the phase a date falls in, so a countdown starts from a real boundary.</summary>
    public static int GetPhaseAnchorDay(CyclePhase phase, int cycleLength)
    {
        var normalizedCycleLength = NormalizeCycleLength(cycleLength);
        var ovulationDay = Math.Max(FirstPossibleOvulationDay,
            normalizedCycleLength - DaysBeforeExpectedPeriodForOvulation);
        return phase switch
        {
            CyclePhase.Menstrual => FirstCycleDay,
            CyclePhase.Follicular => Math.Clamp(FollicularPhaseAnchorDay, FirstPossibleOvulationDay,
                Math.Max(FirstPossibleOvulationDay, ovulationDay - OvulationWindowHalfWidthDays)),
            CyclePhase.Ovulatory => ovulationDay,
            CyclePhase.Luteal => Math.Min(normalizedCycleLength,
                Math.Max(MinimumLutealAnchorDay, ovulationDay + LutealAnchorDaysAfterOvulation)),
            _ => FirstCycleDay
        };
    }

    /// <summary>
    /// Derives the cycle start implied by a single phase observation on a local calendar date.
    /// The observation is the first captured phase (for example from the onboarding quiz), so the
    /// returned start is the day that makes the observed date land on that phase's anchor day.
    /// </summary>
    public static DateOnly GetImpliedCycleStart(CyclePhase phase, DateOnly loggedLocalDate, int cycleLength)
    {
        if (phase is CyclePhase.Unknown || !Enum.IsDefined(phase))
            return loggedLocalDate;

        var anchorDay = GetPhaseAnchorDay(phase, cycleLength);
        return loggedLocalDate.AddDays(FirstCycleDay - anchorDay);
    }

    /// <summary>The phase that follows, wrapping from the last back to the first.</summary>
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

    /// <summary>Derives the phase for a date from that day's manual phase log, falling back to the projection rule.</summary>
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
        var projectedCycleDay = (anchorDay - FirstCycleDay + daysSinceShift) % normalizedCycleLength + FirstCycleDay;
        return CalculatePhase(projectedCycleDay, normalizedCycleLength);
    }
}
