using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CanonicalRecoveryEvidenceRuleTests
{
    private static readonly DateOnly Today = new(2026, 10, 26);
    private static readonly DateOnly FirstWeek = new(2026, 10, 13);
    private static readonly DateOnly SecondWeek = new(2026, 10, 20);

    [Fact]
    public void TwoCompleteWeeksWithMatchingCompoundDeficitsAndPoorRecoveryTriggerDeload()
    {
        var result = CanonicalRecoveryEvidenceRule.Evaluate(Today,
            [new(FirstWeek, 42, 3, 10, 3, 8), new(SecondWeek, 42, 3, 10, 3, 9)],
            [new(FirstWeek, 1, null, null), new(SecondWeek, null, null, 5)]);
        Assert.True(result.IsDeloadTriggered);
    }

    [Fact]
    public void MissingWeekDifferentExerciseOrInsufficientSetsDoNotInventEvidence()
    {
        var checks = new[] { new ObservedRecovery(FirstWeek, 1, null, null), new ObservedRecovery(SecondWeek, 1, null, null) };
        Assert.False(CanonicalRecoveryEvidenceRule.Evaluate(Today,
            [new(FirstWeek, 42, 3, 10, 3, 8)], checks).IsDeloadTriggered);
        Assert.False(CanonicalRecoveryEvidenceRule.Evaluate(Today,
            [new(FirstWeek, 42, 3, 10, 3, 8), new(SecondWeek, 43, 3, 10, 3, 8)], checks).IsDeloadTriggered);
        Assert.False(CanonicalRecoveryEvidenceRule.Evaluate(Today,
            [new(FirstWeek, 42, 3, 10, 2, 8), new(SecondWeek, 42, 3, 10, 3, 8)], checks).IsDeloadTriggered);
    }

    [Fact]
    public void MissingInvalidOrCurrentWeekRecoveryDoesNotTrigger()
    {
        var performances = new[] { new ObservedCompoundPerformance(FirstWeek, 42, 3, 10, 3, 8),
            new ObservedCompoundPerformance(SecondWeek, 42, 3, 10, 3, 8) };
        Assert.False(CanonicalRecoveryEvidenceRule.Evaluate(Today, performances,
            [new(FirstWeek, 0, null, null), new(SecondWeek, null, null, 6), new(Today, 1, null, null)]).IsDeloadTriggered);
    }
}
