using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutInjuryProvenanceRuleTests
{
    [Fact]
    public void OverlappingInjuriesUseLatestClearanceWithoutResettingOnDelayedRestoration()
    {
        var first = new DateOnly(2026, 2, 1);
        var second = new DateOnly(2026, 2, 4);
        var requiredFrom = new DateOnly(2026, 2, 12);
        DateOnly?[] clearances = [first, second];

        Assert.Equal(requiredFrom, WorkoutInjuryClearanceRule.RequiredFromForAllClearances(clearances));
        Assert.True(WorkoutInjuryClearanceRule.CanRestoreForAllClearances(clearances, true, requiredFrom));
    }

    [Fact]
    public void UnknownOrActiveInjuryAndUnsafeConstraintsBlockRestoration()
    {
        var cleared = new DateOnly(2026, 2, 1);
        var today = new DateOnly(2026, 2, 12);
        Assert.False(WorkoutInjuryClearanceRule.CanRestoreForAllClearances([cleared, null], true, today));
        Assert.False(WorkoutInjuryClearanceRule.CanRestoreForAllClearances([], true, today));
        Assert.False(WorkoutInjuryClearanceRule.CanRestoreForAllClearances([cleared], false, today));
    }
}
