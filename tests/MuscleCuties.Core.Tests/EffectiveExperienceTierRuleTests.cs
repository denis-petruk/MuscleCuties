using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class EffectiveExperienceTierRuleTests
{
    private const string ModalityId = "GeneralStrength";
    private static readonly DateOnly ReferenceDate = new(2026, 10, 24);

    [Fact]
    public void MissingHistory_DowngradesToTier1()
    {
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Advanced,
            ModalityId,
            null,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier1BeginnerOrNew, result.Tier);
        Assert.Equal(EffectiveExperienceTierRule.ModalityTierNew, result.ModalityTier);
        Assert.Equal(EffectiveExperienceTierRule.GlobalTierAdvanced, result.GlobalTier);
        Assert.Contains(EffectiveExperienceTierRule.ReasonDowngradedWeakOrMissingHistory, result.ReasonCodes);
    }

    [Theory]
    [InlineData(0, 3.0)]
    [InlineData(12, 0.5)]
    public void NearZeroRecentExposure_DowngradesToTier1(int weeks, double avg)
    {
        var history = new ActivityTrainingHistory(ModalityId, weeks, (decimal)avg, ReferenceDate.AddDays(-2), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Advanced,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier1BeginnerOrNew, result.Tier);
        Assert.Contains(EffectiveExperienceTierRule.ReasonDowngradedNearZeroExposure, result.ReasonCodes);
    }

    [Fact]
    public void ConsistentWeeksUnder4_DowngradesToTier1()
    {
        var history = new ActivityTrainingHistory(ModalityId, 3, 2.0m, ReferenceDate.AddDays(-2), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Advanced,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier1BeginnerOrNew, result.Tier);
        Assert.Contains(EffectiveExperienceTierRule.ReasonDowngradedWeakOrMissingHistory, result.ReasonCodes);
    }

    [Fact]
    public void ReturningActivity_DowngradesToTier1()
    {
        var history = new ActivityTrainingHistory(ModalityId, 16, 3.0m, ReferenceDate.AddDays(-35), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Advanced,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier1BeginnerOrNew, result.Tier);
        Assert.Equal(EffectiveExperienceTierRule.ModalityTierReturning, result.ModalityTier);
        Assert.Contains(EffectiveExperienceTierRule.ReasonDowngradedReturningHistory, result.ReasonCodes);
    }

    [Fact]
    public void EstablishedHistory_WithBeginnerProfile_CappedAtTier1()
    {
        var history = new ActivityTrainingHistory(ModalityId, 14, 2.0m, ReferenceDate.AddDays(-2), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Beginner,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier1BeginnerOrNew, result.Tier);
        Assert.True(result.UsesBeginnerCap);
        Assert.Contains(EffectiveExperienceTierRule.ReasonBeginnerProfileCapApplied, result.ReasonCodes);
    }

    [Fact]
    public void EstablishedHistory_WithStrongBeginnerOverride_UpgradesToTier2()
    {
        var history = new ActivityTrainingHistory(ModalityId, 26, 3.5m, ReferenceDate.AddDays(-2), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Beginner,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier2IntermediateOrEstablished, result.Tier);
        Assert.False(result.UsesBeginnerCap);
        Assert.Contains(EffectiveExperienceTierRule.ReasonStrongHistoryUpgradesBeginner, result.ReasonCodes);
    }

    [Fact]
    public void EstablishedHistory_WithIntermediateProfile_ResolvesTier2()
    {
        var history = new ActivityTrainingHistory(ModalityId, 16, 2.0m, ReferenceDate.AddDays(-2), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Intermediate,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier2IntermediateOrEstablished, result.Tier);
        Assert.Contains(EffectiveExperienceTierRule.ReasonResolvedByHistoryAndProfile, result.ReasonCodes);
    }

    [Fact]
    public void EstablishedHistory_WithStrongIntermediateOverride_UpgradesToTier3()
    {
        var history = new ActivityTrainingHistory(ModalityId, 85, 3.5m, ReferenceDate.AddDays(-2), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Intermediate,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier3Advanced, result.Tier);
        Assert.Contains(EffectiveExperienceTierRule.ReasonStrongHistoryUpgradesIntermediate, result.ReasonCodes);
    }

    [Fact]
    public void EstablishedHistory_WithAdvancedProfile_ResolvesTier3()
    {
        var history = new ActivityTrainingHistory(ModalityId, 16, 2.5m, ReferenceDate.AddDays(-2), true);
        var result = EffectiveExperienceTierRule.Resolve(
            TrainingExperienceLevel.Advanced,
            ModalityId,
            history,
            ReferenceDate);

        Assert.Equal(EffectiveExperienceTier.Tier3Advanced, result.Tier);
        Assert.Equal(EffectiveExperienceTierRule.GlobalTierAdvanced, result.GlobalTier);
        Assert.Equal(EffectiveExperienceTierRule.ModalityTierEstablished, result.ModalityTier);
        Assert.Contains(EffectiveExperienceTierRule.ReasonResolvedByHistoryAndProfile, result.ReasonCodes);
    }
}
