using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CanonicalOccurrenceAdjustmentRuleTests
{
    [Fact]
    public void DeloadIsLimitedToSevenDaysAndPreservesMinimumSet()
    {
        var start = new DateOnly(2026, 10, 26);
        var end = start.AddDays(CanonicalOccurrenceAdjustmentRule.DeloadDurationDays);
        Assert.False(CanonicalOccurrenceAdjustmentRule.IsInDeloadWindow(start.AddDays(-1), start, end));
        Assert.True(CanonicalOccurrenceAdjustmentRule.IsInDeloadWindow(start, start, end));
        Assert.True(CanonicalOccurrenceAdjustmentRule.IsInDeloadWindow(end.AddDays(-1), start, end));
        Assert.False(CanonicalOccurrenceAdjustmentRule.IsInDeloadWindow(end, start, end));
        Assert.Equal((2, 3), CanonicalOccurrenceAdjustmentRule.Deload(3, 2));
        Assert.Equal((1, 5), CanonicalOccurrenceAdjustmentRule.Deload(1, 5));
    }
}
