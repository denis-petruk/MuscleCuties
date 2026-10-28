using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CycleReadinessSafetyRuleTests
{
    private const int Mild = 1;
    private const int Increased = 2;
    private const int PreviousDayOffset = -1;
    private static readonly DateOnly Today = new(2026, 10, 28);

    [Fact]
    public void UnexplainedOrIncreasedPainRequiresReview()
    {
        Assert.True(CycleReadinessSafetyRule.Evaluate(Today, CyclePhase.Unknown, false, Mild, null, []).RequiresReview);
        Assert.True(CycleReadinessSafetyRule.Evaluate(Today, CyclePhase.Unknown, false, Increased, Mild, []).RequiresReview);
        Assert.False(CycleReadinessSafetyRule.Evaluate(Today, CyclePhase.Unknown, false, Mild, Mild, []).RequiresReview);
    }

    [Fact]
    public void FamiliarMenstrualCrampsAreRoutineButNewOrWorseningSymptomsNeedReview()
    {
        var prior = new CycleSafetySymptom(Today.AddDays(PreviousDayOffset), "cramps", Mild);
        var current = new CycleSafetySymptom(Today, "cramps", Mild);
        var familiar = CycleReadinessSafetyRule.Evaluate(Today, CyclePhase.Menstrual, true, null, null, [prior, current]);
        Assert.False(familiar.RequiresReview);
        Assert.True(familiar.RoutineSymptoms);
        Assert.True(CycleReadinessSafetyRule.Evaluate(Today, CyclePhase.Unknown, false, null, null, [prior, current]).RequiresReview);
        Assert.True(CycleReadinessSafetyRule.Evaluate(Today, CyclePhase.Menstrual, true, null, null,
            [prior, current with { Severity = Increased }]).RequiresReview);
        Assert.True(CycleReadinessSafetyRule.Evaluate(Today, CyclePhase.Menstrual, true, null, null, [current]).RequiresReview);
    }
}
