using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class LowerBodyCoverageRuleTests
{
    [Fact]
    public void CountsSessionExposuresForRequiredLowerBodyMuscles()
    {
        var result = LowerBodyCoverageRule.Evaluate([
            new[] { "Glutes", "Hamstrings", "Quads" },
            new[] { "GluteMed", "GluteMax" },
            new[] { "Adductors" }]);

        Assert.Equal(2, result.SessionExposures[LowerBodyMuscleGroup.Glutes]);
        Assert.Equal(1, result.SessionExposures[LowerBodyMuscleGroup.Hamstrings]);
        Assert.Equal(1, result.SessionExposures[LowerBodyMuscleGroup.Abductors]);
        Assert.Equal(1, result.SessionExposures[LowerBodyMuscleGroup.Adductors]);
        Assert.Equal(new[]
        {
            LowerBodyMuscleGroup.Hamstrings, LowerBodyMuscleGroup.Abductors, LowerBodyMuscleGroup.Adductors
        }, result.BelowMinimum);
        Assert.False(result.MeetsMinimum);
    }

    [Fact]
    public void MetMinimumWhenEveryRequiredMuscleAppearsTwice()
    {
        var result = LowerBodyCoverageRule.Evaluate([
            new[] { "Glutes", "Hamstrings", "GluteMed", "Adductors" },
            new[] { "GluteMax", "Hamstring", "HipAbductors", "HipAdductors" }]);

        Assert.All(LowerBodyCoverageRule.MuscleGroups, muscle =>
            Assert.Equal(LowerBodyCoverageRule.MinimumWeeklySessionExposures, result.SessionExposures[muscle]));
        Assert.True(result.MeetsMinimum);
        Assert.Empty(result.BelowMinimum);
    }

    [Fact]
    public void ReportsOneGapCodePerUnderCoveredMuscle()
    {
        Assert.Equal("LowerBodyCoverageBelowMinimum_Adductors",
            LowerBodyCoverageRule.GapCode(LowerBodyMuscleGroup.Adductors));
    }
}
