using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Records the proposed sequence as part of each new plan; it does not infer legacy order.</summary>
public static class WorkoutSessionOrderingRule
{
    public const int FirstSessionOrder = 1;

    private const int StrengthRank = 0;
    private const int CardioOrCompanionRank = 1;
    private const int RecoveryRank = 2;

    public static IReadOnlyList<MixedActivityScheduledSession> Assign(
        IReadOnlyList<MixedActivityScheduledSession> sessions)
    {
        var nextByDay = new Dictionary<DayOfWeek, int>();
        return sessions.OrderBy(session => session.Day)
            .ThenBy(session => SessionRank(session.Activity)).Select(session =>
        {
            var next = nextByDay.GetValueOrDefault(session.Day, FirstSessionOrder);
            nextByDay[session.Day] = checked(next + 1);
            return session with { DayOrder = next };
        }).ToArray();
    }

    public static bool IsValid(IReadOnlyCollection<MixedActivityScheduledSession> sessions) =>
        sessions.GroupBy(session => session.Day).All(day =>
            day.Select(session => session.DayOrder).OrderBy(order => order)
                .SequenceEqual(Enumerable.Range(FirstSessionOrder, day.Count()).Select(order => (int?)order)));

    /// <summary>
    /// Owner rule: cardio only ever follows the strength session on that day, and recovery or
    /// mobility work closes the day after both.
    /// </summary>
    private static int SessionRank(WorkoutActivityType activity) => IsStrength(activity)
        ? StrengthRank
        : IsRecovery(activity) ? RecoveryRank : CardioOrCompanionRank;

    private static bool IsStrength(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.StrengthHighIntensity;

    private static bool IsRecovery(WorkoutActivityType activity) => activity is
        WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery;
}
