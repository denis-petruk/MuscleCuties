using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthScheduleAssignmentRuleTests
{
    private const int TwoSessions = 2;
    private static readonly IReadOnlySet<string> Legs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "legs" };
    private static readonly IReadOnlySet<string> Arms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "arms" };

    [Fact]
    public void ReassignsSessionsToAvoidConsecutiveOverlapAcrossWeekBoundary()
    {
        var result = StrengthScheduleAssignmentRule.Assign([Legs, Legs],
            [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Wednesday], [],
            WorkoutActivityType.GeneralStrength, true, TwoSessions);

        Assert.Equal(TwoSessions, result.Assignments.Count);
        Assert.False(result.Assignments.Any(item => item.Day == DayOfWeek.Sunday) &&
            result.Assignments.Any(item => item.Day == DayOfWeek.Monday));
        Assert.Empty(result.Limitations);
    }

    [Fact]
    public void ReducesSessionsWhenNoSafePairExists()
    {
        var result = StrengthScheduleAssignmentRule.Assign([Legs, Legs],
            [DayOfWeek.Sunday, DayOfWeek.Monday], [],
            WorkoutActivityType.GeneralStrength, true, TwoSessions);

        Assert.Single(result.Assignments);
        Assert.Contains(StrengthScheduleAssignmentRule.SessionsReducedForMuscleRecovery, result.Limitations);
    }

    [Fact]
    public void DistinctMusclesCanTrainOnConsecutiveDaysAndMissingMetadataBlocks()
    {
        var valid = StrengthScheduleAssignmentRule.Assign([Legs, Arms],
            [DayOfWeek.Sunday, DayOfWeek.Monday], [], WorkoutActivityType.GeneralStrength, true, TwoSessions);
        var missing = StrengthScheduleAssignmentRule.Assign([Legs, null],
            [DayOfWeek.Sunday, DayOfWeek.Monday], [], WorkoutActivityType.GeneralStrength, true, TwoSessions);

        Assert.Equal(TwoSessions, valid.Assignments.Count);
        Assert.Empty(missing.Assignments);
        Assert.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing, missing.Limitations);
    }

    [Fact]
    public void Reconcile_never_leaves_three_sessions_on_a_day_it_reassigns()
    {
        // Two companions already share Friday: reassigning a strength session onto it would create
        // the three-session day the accept path refuses with ProposalMixedScheduleInvalid.
        var schedule = new MixedActivityWeekScheduleResult(
            [
                new(WorkoutActivityType.HighVolumeStrength, DayOfWeek.Monday, 60m, true),
                new(WorkoutActivityType.HighVolumeStrength, DayOfWeek.Thursday, 60m, true),
                new(WorkoutActivityType.Swimming, DayOfWeek.Friday, 60m, false),
                new(WorkoutActivityType.Yoga, DayOfWeek.Friday, 15m, false)
            ],
            [
                Decision(WorkoutActivityType.HighVolumeStrength, TwoSessions),
                Decision(WorkoutActivityType.Swimming, 1),
                Decision(WorkoutActivityType.Yoga, 1)
            ],
            []);

        var result = StrengthScheduleAssignmentRule.Reconcile(schedule, [Legs, Arms],
            [DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
            WorkoutActivityType.HighVolumeStrength);

        Assert.All(result.Sessions.GroupBy(session => session.Day), day =>
            Assert.True(day.Count() <= MixedActivityWeekScheduleRule.MaximumSessionsPerDay,
                $"{day.Key} carries {day.Count()} sessions"));
        Assert.Empty(WorkoutScheduleValidationRule.Validate(
            result.Sessions.Select(session => new WorkoutScheduleValidationSession(
                session.Day, session.Activity, session.IsHardSession)).ToArray(),
            requireStrengthMuscles: false));
    }

    private static MixedActivityScheduleDecision Decision(WorkoutActivityType activity, int scheduled) =>
        new(activity, MixedActivityScheduleDisposition.Scheduled, null, null, scheduled, scheduled, scheduled, []);
}
