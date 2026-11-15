using System.Reflection;
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
            [new(FirstWeek, 1, null), new(SecondWeek, 1, null)]);
        Assert.True(result.IsDeloadTriggered);
    }

    [Fact]
    public void Recovery_evidence_cannot_carry_a_soreness_rating()
    {
        // A-8: soreness is not a recovery input. The check previously fed soreness 1 vs 5 through this
        // path; the field is gone, so the invariance is now enforced by the type itself.
        Assert.DoesNotContain(typeof(ObservedRecovery).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Soreness", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingWeekDifferentExerciseOrInsufficientSetsDoNotInventEvidence()
    {
        var checks = new[] { new ObservedRecovery(FirstWeek, 1, null), new ObservedRecovery(SecondWeek, 1, null) };
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
            [new(FirstWeek, 0, null), new(SecondWeek, null, null), new(Today, 1, null)]).IsDeloadTriggered);
    }
}
