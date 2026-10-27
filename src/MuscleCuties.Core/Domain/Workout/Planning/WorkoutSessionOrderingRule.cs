namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Records the proposed sequence as part of each new plan; it does not infer legacy order.</summary>
public static class WorkoutSessionOrderingRule
{
    public const int FirstSessionOrder = 1;

    public static IReadOnlyList<MixedActivityScheduledSession> Assign(
        IReadOnlyList<MixedActivityScheduledSession> sessions)
    {
        var nextByDay = new Dictionary<DayOfWeek, int>();
        return sessions.Select(session =>
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
}
