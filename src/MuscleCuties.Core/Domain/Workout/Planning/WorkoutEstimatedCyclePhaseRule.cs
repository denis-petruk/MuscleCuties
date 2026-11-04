using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Uses only recorded menstrual starts. A phase is suitable for a workout adjustment
/// when all recent completed intervals agree on that phase before the next possible start.
/// Profile and default cycle lengths are intentionally excluded.
/// </summary>
public static class WorkoutEstimatedCyclePhaseRule
{
    public const int RequiredCompletedIntervals = CycleReadinessPrescriptionRule.MinimumDistinctCyclesForPattern;
    public const int RequiredRecordedStarts = RequiredCompletedIntervals + 1;
    private const int FirstCycleDay = 1;

    public static CyclePhase Estimate(DateOnly targetDate, DateOnly observedThrough,
        IReadOnlyList<DateOnly>? recordedMenstrualStarts)
    {
        if (targetDate < observedThrough || recordedMenstrualStarts is null ||
            recordedMenstrualStarts.Count < RequiredRecordedStarts)
            return CyclePhase.Unknown;

        var starts = recordedMenstrualStarts.OrderByDescending(date => date).Take(RequiredRecordedStarts)
            .OrderBy(date => date).ToArray();
        if (starts[^1] > observedThrough || starts.Distinct().Count() != RequiredRecordedStarts ||
            targetDate < starts[^1])
            return CyclePhase.Unknown;

        var intervals = starts.Zip(starts.Skip(1), (first, next) => next.DayNumber - first.DayNumber).ToArray();
        if (intervals.Length != RequiredCompletedIntervals || intervals.Any(length =>
                length is < CycleLengthLimits.Minimum or > CycleLengthLimits.Maximum))
            return CyclePhase.Unknown;

        var elapsedDays = targetDate.DayNumber - starts[^1].DayNumber;
        if (intervals.Any(length => elapsedDays >= length))
            return CyclePhase.Unknown;

        var phase = CyclePhaseRule.CalculatePhase(elapsedDays + FirstCycleDay, intervals[0]);
        return phase != CyclePhase.Unknown && intervals.All(length =>
            CyclePhaseRule.CalculatePhase(elapsedDays + FirstCycleDay, length) == phase)
            ? phase : CyclePhase.Unknown;
    }
}
