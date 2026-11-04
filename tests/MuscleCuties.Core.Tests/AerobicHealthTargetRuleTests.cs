using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class AerobicHealthTargetRuleTests
{
    [Fact]
    public void CalculateSessionModerateEquivalent_AppliesVigorousMultiplier()
    {
        // 30 min moderate + 20 min vigorous = 30 + 2 * 20 = 70 ME minutes
        var me = AerobicHealthTargetRule.CalculateSessionModerateEquivalentMinutes(30m, 20m);
        Assert.Equal(70m, me);
    }

    [Fact]
    public void Evaluate_Below150Minutes_ReturnsBelowGuideline()
    {
        var sessions = new[]
        {
            new AerobicSessionMinutes(30m, 15m), // 30 + 30 = 60
            new AerobicSessionMinutes(40m, 10m)  // 40 + 20 = 60 => total 120
        };

        var result = AerobicHealthTargetRule.Evaluate(sessions, resistanceTrainingDaysPerWeek: 1);

        Assert.Equal(120m, result.TotalModerateEquivalentMinutes);
        Assert.False(result.MeetsAerobicTarget);
        Assert.False(result.MeetsResistanceTarget);
        Assert.False(result.MeetsAllTargets);
        Assert.Equal(AerobicHealthTargetRule.StatusBelowGuideline, result.AerobicGuidelineStatus);
        Assert.Equal(AerobicHealthTargetRule.ResistanceTargetNotMet, result.ResistanceGuidelineStatus);
    }

    [Fact]
    public void Evaluate_Between150And300MinutesAndTwoResistanceDays_MeetsAllTargets()
    {
        var sessions = new[]
        {
            new AerobicSessionMinutes(60m, 30m), // 60 + 60 = 120
            new AerobicSessionMinutes(60m, 10m)  // 60 + 20 = 80 => total 200
        };

        var result = AerobicHealthTargetRule.Evaluate(sessions, resistanceTrainingDaysPerWeek: 2);

        Assert.Equal(200m, result.TotalModerateEquivalentMinutes);
        Assert.True(result.MeetsAerobicTarget);
        Assert.True(result.MeetsResistanceTarget);
        Assert.True(result.MeetsAllTargets);
        Assert.Equal(AerobicHealthTargetRule.StatusMeetsGuideline, result.AerobicGuidelineStatus);
        Assert.Equal(AerobicHealthTargetRule.ResistanceTargetMet, result.ResistanceGuidelineStatus);
    }

    [Fact]
    public void Evaluate_Over300Minutes_ReturnsExceedsGuideline()
    {
        var sessions = new[]
        {
            new AerobicSessionMinutes(150m, 100m) // 150 + 200 = 350
        };

        var result = AerobicHealthTargetRule.Evaluate(sessions, resistanceTrainingDaysPerWeek: 3);

        Assert.Equal(350m, result.TotalModerateEquivalentMinutes);
        Assert.True(result.MeetsAerobicTarget);
        Assert.True(result.MeetsResistanceTarget);
        Assert.True(result.MeetsAllTargets);
        Assert.Equal(AerobicHealthTargetRule.StatusExceedsGuideline, result.AerobicGuidelineStatus);
    }
}
