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
}
