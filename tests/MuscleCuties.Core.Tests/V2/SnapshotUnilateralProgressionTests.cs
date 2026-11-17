using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using Xunit;

namespace MuscleCuties.Core.Tests.V2;

public sealed class SnapshotUnilateralProgressionTests
{
    [Fact]
    public void MissingSideRepetitions_DoNotEarnAUnilateralLoadIncrease()
    {
        var exposures = new[]
        {
            Exposure(new DateTime(2026, 11, 2), left: 12, right: 12),
            Exposure(new DateTime(2026, 11, 9), left: null, right: 12)
        };

        var advice = SnapshotProgressionRule.Resolve(
            MajorMuscleGroup.Glutes,
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            exerciseId: 404,
            plannedReps: 10,
            currentLoad: 20m,
            exposures);

        Assert.False(advice.LoadIncreased);
        Assert.NotEqual(StrengthProgressionAdviceRule.EarnedCode, advice.RecommendationCode);
    }

    [Fact]
    public void UnilateralProgressionUsesTheWeakerSideRatherThanTotalRepetitions()
    {
        var exposures = new[]
        {
            Exposure(new DateTime(2026, 11, 2), left: 12, right: 12),
            Exposure(new DateTime(2026, 11, 9), left: 8, right: 12)
        };

        var advice = SnapshotProgressionRule.Resolve(
            MajorMuscleGroup.Glutes,
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            exerciseId: 404,
            plannedReps: 10,
            currentLoad: 20m,
            exposures);

        Assert.False(advice.LoadIncreased);
        Assert.NotEqual(StrengthProgressionAdviceRule.EarnedCode, advice.RecommendationCode);
    }

    [Fact]
    public void UnilateralRepRangeRequiresTheMaximumOnBothSidesForAnIncrease()
    {
        var lowerRangeReps = new[]
        {
            ExposureWithRange(new DateTime(2026, 11, 2), 8, 8),
            ExposureWithRange(new DateTime(2026, 11, 9), 8, 8)
        };
        var topRangeReps = new[]
        {
            ExposureWithRange(new DateTime(2026, 11, 2), 12, 12),
            ExposureWithRange(new DateTime(2026, 11, 9), 12, 12)
        };

        var belowTop = SnapshotProgressionRule.Resolve(
            MajorMuscleGroup.Glutes,
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            exerciseId: 404,
            plannedReps: 8,
            currentLoad: 20m,
            lowerRangeReps);
        var atTop = SnapshotProgressionRule.Resolve(
            MajorMuscleGroup.Glutes,
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            exerciseId: 404,
            plannedReps: 8,
            currentLoad: 20m,
            topRangeReps);

        Assert.False(belowTop.LoadIncreased);
        Assert.Equal(StrengthProgressionAdviceRule.EarnedCode, atTop.RecommendationCode);
        Assert.True(atTop.LoadIncreased);
    }

    private static SnapshotProgressionExposure Exposure(DateTime completedAt, int? left, int? right) =>
        new(completedAt, 1, 10, 2,
            [new SnapshotProgressionSet(1, 12, 20m, 2, true, left, right)], IsUnilateral: true);

    private static SnapshotProgressionExposure ExposureWithRange(DateTime completedAt, int left, int right) =>
        new(completedAt, 1, 8, 2,
            [new SnapshotProgressionSet(1, Math.Min(left, right), 20m, 2, true, left, right)],
            IsUnilateral: true, MaximumPrescribedRepetitions: 12);
}
