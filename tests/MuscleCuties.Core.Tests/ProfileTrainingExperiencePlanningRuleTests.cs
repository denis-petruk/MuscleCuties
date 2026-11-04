using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Tests;

public sealed class ProfileTrainingExperiencePlanningRuleTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void BeginnerProfileCapsEstablishedActivityPrescriptionWithoutChangingActivityClassification()
    {
        var decision = ProfileTrainingExperiencePlanningRule.Resolve(
            ActivityExperience.Established, TrainingExperienceLevel.Beginner);

        Assert.Equal(ActivityExperience.Established, decision.ActivityHistoryExperience);
        Assert.Equal(ActivityExperience.Beginner, decision.PrescriptionExperience);
        Assert.True(decision.UsesConservativeProfileCap);
        Assert.Equal(TrainingExperienceLevel.Beginner, decision.EffectiveExperienceLevel);
        Assert.Equal(ProfileTrainingExperiencePlanningRule.BeginnerMaximumWeeklySetsPerMuscle,
            decision.MaximumWeeklySetsPerMuscle);
        Assert.Contains(ProfileTrainingExperiencePlanningRule.BeginnerProfileCapReason, decision.ReasonCodes);
    }

    [Theory]
    [InlineData(TrainingExperienceLevel.Intermediate)]
    [InlineData(TrainingExperienceLevel.Advanced)]
    [InlineData(TrainingExperienceLevel.Unknown)]
    public void ProfileLevelCannotUpgradeActivitySpecificHistory(
        TrainingExperienceLevel profileExperience)
    {
        var decision = ProfileTrainingExperiencePlanningRule.Resolve(
            ActivityExperience.Beginner, profileExperience);

        Assert.Equal(ActivityExperience.Beginner, decision.ActivityHistoryExperience);
        Assert.Equal(ActivityExperience.Beginner, decision.PrescriptionExperience);
        Assert.False(decision.UsesConservativeProfileCap);
    }

    [Fact]
    public void MissingActivityHistoryRemainsUnknownEvenWhenProfileSaysBeginner()
    {
        var historyDecision = ActivityExperienceRule.Classify("swimming", null, Today);
        var planningDecision = ProfileTrainingExperiencePlanningRule.Resolve(
            historyDecision.Experience, TrainingExperienceLevel.Beginner);

        Assert.Equal(ActivityExperience.Unknown, historyDecision.Experience);
        Assert.Equal(ActivityExperience.Unknown, planningDecision.PrescriptionExperience);
        Assert.False(planningDecision.UsesConservativeProfileCap);
    }

    [Fact]
    public void FallbackUsesProfileTrainingExperienceWhenHistoryIsMissing()
    {
        var intermediate = ProfileTrainingExperiencePlanningRule.Resolve(
            ActivityExperience.Unknown,
            TrainingExperienceLevel.Intermediate,
            history: null,
            allowProfileFallbackWhenHistoryMissing: true);
        var advanced = ProfileTrainingExperiencePlanningRule.Resolve(
            ActivityExperience.Unknown,
            TrainingExperienceLevel.Advanced,
            history: null,
            allowProfileFallbackWhenHistoryMissing: true);

        Assert.Equal(ActivityExperience.Established, intermediate.PrescriptionExperience);
        Assert.Equal(TrainingExperienceLevel.Intermediate, intermediate.EffectiveExperienceLevel);
        Assert.Equal(ProfileTrainingExperiencePlanningRule.IntermediateMaximumWeeklySetsPerMuscle,
            intermediate.MaximumWeeklySetsPerMuscle);
        Assert.Contains(ProfileTrainingExperiencePlanningRule.ProfileExperienceFallbackReason,
            intermediate.ReasonCodes);

        Assert.Equal(ActivityExperience.Established, advanced.PrescriptionExperience);
        Assert.Equal(TrainingExperienceLevel.Advanced, advanced.EffectiveExperienceLevel);
        Assert.Equal(ProfileTrainingExperiencePlanningRule.AdvancedMaximumWeeklySetsPerMuscle,
            advanced.MaximumWeeklySetsPerMuscle);
        Assert.True(advanced.AllowsAggressivePeriodization);
    }

    [Fact]
    public void AdvancedProfileDowngradesToBeginnerWhenRecentExposureIsNearZero()
    {
        var nearZeroHistory = new ActivityTrainingHistory("GeneralStrength", 0, 0.5m, Today.AddDays(-3), true);
        var classified = ActivityExperienceRule.Classify("GeneralStrength", nearZeroHistory, Today);

        var decision = ProfileTrainingExperiencePlanningRule.Resolve(
            classified.Experience,
            TrainingExperienceLevel.Advanced,
            nearZeroHistory);

        Assert.Equal(ActivityExperience.Beginner, decision.PrescriptionExperience);
        Assert.Equal(TrainingExperienceLevel.Beginner, decision.EffectiveExperienceLevel);
        Assert.Equal(ProfileTrainingExperiencePlanningRule.BeginnerMaximumWeeklySetsPerMuscle,
            decision.MaximumWeeklySetsPerMuscle);
        Assert.Contains(ProfileTrainingExperiencePlanningRule.NearZeroRecentExposureDowngradeReason,
            decision.ReasonCodes);
    }

    [Fact]
    public void BeginnerProfileWithStrongLongTermHistoryAllowsIntermediateProgressionWithBeginnerVolumeCeiling()
    {
        var strongHistory = new ActivityTrainingHistory(
            "GeneralStrength",
            ProfileTrainingExperiencePlanningRule.StrongBeginnerOverrideConsistentWeeks,
            ProfileTrainingExperiencePlanningRule.StrongBeginnerOverrideAverageSessionsPerWeek,
            Today.AddDays(-2),
            true);
        var classified = ActivityExperienceRule.Classify("GeneralStrength", strongHistory, Today);

        var decision = ProfileTrainingExperiencePlanningRule.Resolve(
            classified.Experience,
            TrainingExperienceLevel.Beginner,
            strongHistory);

        Assert.Equal(ActivityExperience.Established, decision.PrescriptionExperience);
        Assert.False(decision.UsesConservativeProfileCap);
        Assert.Equal(TrainingExperienceLevel.Intermediate, decision.EffectiveExperienceLevel);
        Assert.Equal(ProfileTrainingExperiencePlanningRule.BeginnerMaximumWeeklySetsPerMuscle,
            decision.MaximumWeeklySetsPerMuscle);
        Assert.Contains(ProfileTrainingExperiencePlanningRule.StrongHistoryUpgradesBeginnerProfileReason,
            decision.ReasonCodes);
    }

    [Fact]
    public void IntermediateAndAdvancedProfilesDifferentiateCapsOnceEstablished()
    {
        var establishedHistory = new ActivityTrainingHistory("GeneralStrength", 16, 3m, Today.AddDays(-2), true);

        var intermediate = ProfileTrainingExperiencePlanningRule.Resolve(
            ActivityExperience.Established,
            TrainingExperienceLevel.Intermediate,
            establishedHistory);
        var advanced = ProfileTrainingExperiencePlanningRule.Resolve(
            ActivityExperience.Established,
            TrainingExperienceLevel.Advanced,
            establishedHistory);

        Assert.Equal(ProfileTrainingExperiencePlanningRule.IntermediateMaximumResistanceDaysPerWeek,
            intermediate.MaximumResistanceDaysPerWeek);
        Assert.Equal(ProfileTrainingExperiencePlanningRule.IntermediateMaximumWeeklySetsPerMuscle,
            intermediate.MaximumWeeklySetsPerMuscle);
        Assert.False(intermediate.AllowsAggressivePeriodization);

        Assert.Equal(ProfileTrainingExperiencePlanningRule.AdvancedMaximumResistanceDaysPerWeek,
            advanced.MaximumResistanceDaysPerWeek);
        Assert.Equal(ProfileTrainingExperiencePlanningRule.AdvancedMaximumWeeklySetsPerMuscle,
            advanced.MaximumWeeklySetsPerMuscle);
        Assert.True(advanced.AllowsAggressivePeriodization);
    }
}
