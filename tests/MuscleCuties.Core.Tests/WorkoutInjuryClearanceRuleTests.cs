using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutInjuryClearanceRuleTests
{
    private const int LastDayBeforeRequiredOffset = -1;
    private const int LaterClearanceOffset = 2;
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
    [Fact]
    public void CalendarWindowCrossesLeapDayWithoutRestarting()
    {
        var cleared = DateOnly.Parse("2028-02-26");
        var requiredFrom = DateOnly.Parse("2028-03-05");
        Assert.Equal(requiredFrom, WorkoutInjuryClearanceRule.RequiredFromLocalDate(cleared));
        Assert.True(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(cleared, requiredFrom.AddDays(LastDayBeforeRequiredOffset)));
        Assert.False(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(cleared, requiredFrom));
    }

    [Fact]
    public void OverlappingEpisodesUseLatestClearanceWithoutResettingIt()
    {
        var laterClearance = ClearanceDate.AddDays(LaterClearanceOffset);
        var episodes = new[] { new InjuryRestorationEpisode(false, ClearanceDate), new InjuryRestorationEpisode(false, laterClearance) };
        Assert.Equal(InjuryClearanceRestorationDisposition.RestoreOptional,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true, episodes, RequiredDate));
        Assert.Equal(InjuryClearanceRestorationDisposition.RestoreRequired,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true, episodes, laterClearance.AddDays(WorkoutInjuryClearanceRule.FollowingMidnightOffsetDays)));
        Assert.Equal(InjuryClearanceRestorationDisposition.RestoreOptional,
            WorkoutInjuryClearanceRule.EvaluateRestoration(true, true, episodes, DelayedRestorationDate));
        Assert.Equal(InjuryClearanceRestorationDisposition.Blocked,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true, [new(true, null)], DelayedRestorationDate));
        Assert.Equal(InjuryClearanceRestorationDisposition.Blocked,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, false, episodes, DelayedRestorationDate));
    }

    [Fact]
    public void UnknownOrFutureClearanceNeverRestores()
    {
        Assert.Equal(InjuryClearanceRestorationDisposition.Blocked,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true, [new(false, null)], ClearanceDate));
        Assert.Equal(InjuryClearanceRestorationDisposition.Blocked,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true,
                [new(false, ClearanceDate.AddDays(LaterClearanceOffset))], ClearanceDate));
    }

}
