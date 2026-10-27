using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutInjuryClearanceRuleTests
{
    private static readonly DateOnly ClearanceDate = DateOnly.Parse("2026-10-01");
    private static readonly DateOnly LastOptionalDate = DateOnly.Parse("2026-10-08");
    private static readonly DateOnly RequiredDate = DateOnly.Parse("2026-10-09");
    private static readonly DateOnly DelayedRestorationDate = DateOnly.Parse("2026-10-20");

    [Fact]
    public void ClearanceDayAndSevenFullFollowingDaysAreOptional()
    {
        Assert.Equal(RequiredDate, WorkoutInjuryClearanceRule.RequiredFromLocalDate(ClearanceDate));
        Assert.True(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(ClearanceDate, ClearanceDate));
        Assert.True(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(ClearanceDate, LastOptionalDate));
        Assert.False(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(ClearanceDate, RequiredDate));
    }

    [Fact]
    public void DelayedRestorationDoesNotRestartDeadline()
    {
        Assert.True(WorkoutInjuryClearanceRule.CanRestore(true, ClearanceDate, DelayedRestorationDate));
        Assert.False(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(ClearanceDate, DelayedRestorationDate));
    }

    [Fact]
    public void OverlappingRestrictionOrUnknownProvenanceBlocksRestoration()
    {
        Assert.False(WorkoutInjuryClearanceRule.CanRestore(false, ClearanceDate, RequiredDate));
        Assert.False(WorkoutInjuryClearanceRule.CanRestore(true, null, RequiredDate));
        Assert.Null(WorkoutInjuryClearanceRule.RequiredFromLocalDate(DateOnly.MaxValue));
    }
}
