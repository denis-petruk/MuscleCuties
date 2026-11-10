using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

// S-05: no post-clearance optional window. Restoration is decided by current eligibility from the clearance date.
public sealed class WorkoutInjuryClearanceRuleTests
{
    private const int LaterClearanceOffset = 2;
    private static readonly DateOnly DayBeforeClearance = DateOnly.Parse("2026-09-30");
    private static readonly DateOnly ClearanceDate = DateOnly.Parse("2026-10-01");
    private static readonly DateOnly NextDay = DateOnly.Parse("2026-10-02");
    private static readonly DateOnly DelayedRestorationDate = DateOnly.Parse("2026-10-20");

    [Fact]
    public void ClearanceDayIsRequiredWithoutAnOptionalWindow()
    {
        Assert.Equal(0, WorkoutInjuryClearanceRule.OptionalCalendarDaysIncludingClearance);
        Assert.Equal(ClearanceDate, WorkoutInjuryClearanceRule.RequiredFromLocalDate(ClearanceDate));
        Assert.False(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(ClearanceDate, ClearanceDate));
        Assert.False(WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(ClearanceDate, NextDay));
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
        Assert.False(WorkoutInjuryClearanceRule.CanRestore(false, ClearanceDate, NextDay));
        Assert.False(WorkoutInjuryClearanceRule.CanRestore(true, null, NextDay));
        Assert.False(WorkoutInjuryClearanceRule.CanRestore(true, ClearanceDate, DayBeforeClearance));
        Assert.Equal(DateOnly.MaxValue, WorkoutInjuryClearanceRule.RequiredFromLocalDate(DateOnly.MaxValue));
    }

    [Fact]
    public void OverlappingEpisodesRestoreOnlyAfterEveryClearance()
    {
        var laterClearance = ClearanceDate.AddDays(LaterClearanceOffset);
        var episodes = new[] { new InjuryRestorationEpisode(false, ClearanceDate), new InjuryRestorationEpisode(false, laterClearance) };
        Assert.Equal(InjuryClearanceRestorationDisposition.Blocked,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true, episodes, NextDay));
        Assert.Equal(InjuryClearanceRestorationDisposition.RestoreRequired,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true, episodes, laterClearance));
        Assert.Equal(InjuryClearanceRestorationDisposition.RestoreOptional,
            WorkoutInjuryClearanceRule.EvaluateRestoration(true, true, episodes, DelayedRestorationDate));
        Assert.Equal(InjuryClearanceRestorationDisposition.Blocked,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, true, [new(true, null)], DelayedRestorationDate));
        Assert.Equal(InjuryClearanceRestorationDisposition.Blocked,
            WorkoutInjuryClearanceRule.EvaluateRestoration(false, false, episodes, DelayedRestorationDate));
        Assert.Equal(laterClearance,
            WorkoutInjuryClearanceRule.RequiredFromForAllClearances([ClearanceDate, laterClearance]));
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

    [Fact]
    public void ActiveUnknownOrFutureRestrictionsAreNotDropped()
    {
        Assert.True(WorkoutInjuryClearanceRule.RequiresConservativeRestriction(false, ClearanceDate, DelayedRestorationDate));
        Assert.False(WorkoutInjuryClearanceRule.RequiresConservativeRestriction(true, ClearanceDate, ClearanceDate));
        Assert.True(WorkoutInjuryClearanceRule.RequiresConservativeRestriction(true, null, NextDay));
        Assert.True(WorkoutInjuryClearanceRule.RequiresConservativeRestriction(true, ClearanceDate, null));
        Assert.True(WorkoutInjuryClearanceRule.RequiresConservativeRestriction(true, NextDay, ClearanceDate));
    }
}
