using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum EffectiveExperienceTier
{
    Tier1BeginnerOrNew = 1,
    Tier2IntermediateOrEstablished = 2,
    Tier3Advanced = 3
}

public sealed record EffectiveExperienceTierResult(
    EffectiveExperienceTier Tier,
    int ModalityTier,
    int GlobalTier,
    bool UsesBeginnerCap,
    IReadOnlyList<string> ReasonCodes);

public static class EffectiveExperienceTierRule
{
    public const int MinimumConsistentWeeksForModality = 4;
    public const decimal MinimumAverageSessionsPerWeekForModality = 1m;

    public const int ModalityTierNew = 1;
    public const int ModalityTierEstablished = 2;
    public const int ModalityTierReturning = 1;

    public const int GlobalTierBeginner = 1;
    public const int GlobalTierIntermediate = 2;
    public const int GlobalTierAdvanced = 3;

    public const string ReasonDowngradedNearZeroExposure = "NearZeroExposureDowngradedToTier1";
    public const string ReasonDowngradedWeakOrMissingHistory = "WeakOrMissingHistoryDowngradedToTier1";
    public const string ReasonDowngradedReturningHistory = "ReturningHistoryDowngradedToTier1";
    public const string ReasonBeginnerProfileCapApplied = "BeginnerProfileCapApplied";
    public const string ReasonStrongHistoryUpgradesBeginner = "StrongActivityHistoryUpgradesBeginnerProfile";
    public const string ReasonStrongHistoryUpgradesIntermediate = "StrongActivityHistoryUpgradesIntermediateProfile";
    public const string ReasonResolvedByHistoryAndProfile = "ResolvedByHistoryAndProfile";

    public static EffectiveExperienceTierResult Resolve(
        TrainingExperienceLevel? profileLevel,
        string activityId,
        ActivityTrainingHistory? history,
        DateOnly localDate)
    {
        var classification = ActivityExperienceRule.Classify(activityId, history, localDate);
        return Resolve(profileLevel, classification.Experience, history);
    }

    public static EffectiveExperienceTierResult Resolve(
        TrainingExperienceLevel? profileLevel,
        ActivityExperience activityExperience,
        ActivityTrainingHistory? history = null)
    {
        var globalTier = ResolveGlobalTier(profileLevel);

        // Near-zero exposure or missing/weak consistency check
        if (history is not null &&
            (history.ConsistentParticipationWeeks is <= 0 ||
             history.AverageSessionsPerWeekInRecentFourWeeks < MinimumAverageSessionsPerWeekForModality))
        {
            return new EffectiveExperienceTierResult(
                EffectiveExperienceTier.Tier1BeginnerOrNew,
                ModalityTierNew,
                globalTier,
                UsesBeginnerCap: false,
                [ReasonDowngradedNearZeroExposure]);
        }

        if (activityExperience is ActivityExperience.Unknown || history is null ||
            history.ConsistentParticipationWeeks is null or < MinimumConsistentWeeksForModality ||
            history.AverageSessionsPerWeekInRecentFourWeeks is null or < MinimumAverageSessionsPerWeekForModality)
        {
            return new EffectiveExperienceTierResult(
                EffectiveExperienceTier.Tier1BeginnerOrNew,
                ModalityTierNew,
                globalTier,
                UsesBeginnerCap: false,
                [ReasonDowngradedWeakOrMissingHistory]);
        }

        if (activityExperience is ActivityExperience.Returning)
        {
            return new EffectiveExperienceTierResult(
                EffectiveExperienceTier.Tier1BeginnerOrNew,
                ModalityTierReturning,
                globalTier,
                UsesBeginnerCap: false,
                [ReasonDowngradedReturningHistory]);
        }

        if (activityExperience is ActivityExperience.Beginner)
        {
            return new EffectiveExperienceTierResult(
                EffectiveExperienceTier.Tier1BeginnerOrNew,
                ModalityTierNew,
                globalTier,
                UsesBeginnerCap: false,
                [ReasonDowngradedWeakOrMissingHistory]);
        }

        // Strong history: Established and not Returning
        var modalityTier = ModalityTierEstablished;

        if (profileLevel is TrainingExperienceLevel.Beginner)
        {
            if (IsStrongHistoryForBeginnerUpgrade(history))
            {
                return new EffectiveExperienceTierResult(
                    EffectiveExperienceTier.Tier2IntermediateOrEstablished,
                    modalityTier,
                    globalTier,
                    UsesBeginnerCap: false,
                    [ReasonStrongHistoryUpgradesBeginner]);
            }

            return new EffectiveExperienceTierResult(
                EffectiveExperienceTier.Tier1BeginnerOrNew,
                modalityTier,
                globalTier,
                UsesBeginnerCap: true,
                [ReasonBeginnerProfileCapApplied]);
        }

        if (profileLevel is TrainingExperienceLevel.Intermediate)
        {
            if (IsStrongHistoryForAdvancedModality(history))
            {
                return new EffectiveExperienceTierResult(
                    EffectiveExperienceTier.Tier3Advanced,
                    modalityTier,
                    globalTier,
                    UsesBeginnerCap: false,
                    [ReasonStrongHistoryUpgradesIntermediate]);
            }

            var intermediateTier = Math.Max(modalityTier, globalTier);
            return new EffectiveExperienceTierResult(
                (EffectiveExperienceTier)intermediateTier,
                modalityTier,
                globalTier,
                UsesBeginnerCap: false,
                [ReasonResolvedByHistoryAndProfile]);
        }

        if (profileLevel is TrainingExperienceLevel.Advanced)
        {
            var advancedTier = Math.Max(modalityTier, globalTier);
            return new EffectiveExperienceTierResult(
                (EffectiveExperienceTier)advancedTier,
                modalityTier,
                globalTier,
                UsesBeginnerCap: false,
                [ReasonResolvedByHistoryAndProfile]);
        }

        // Fallback for null/unknown profile level with established history
        var fallbackTier = Math.Max(modalityTier, globalTier);
        return new EffectiveExperienceTierResult(
            (EffectiveExperienceTier)fallbackTier,
            modalityTier,
            globalTier,
            UsesBeginnerCap: false,
            [ReasonResolvedByHistoryAndProfile]);
    }

    private static int ResolveGlobalTier(TrainingExperienceLevel? profileLevel) =>
        profileLevel switch
        {
            TrainingExperienceLevel.Intermediate => GlobalTierIntermediate,
            TrainingExperienceLevel.Advanced => GlobalTierAdvanced,
            _ => GlobalTierBeginner
        };

    private static bool IsStrongHistoryForBeginnerUpgrade(ActivityTrainingHistory? history) =>
        history is
        {
            ConsistentParticipationWeeks: >= ProfileTrainingExperiencePlanningRule.StrongBeginnerOverrideConsistentWeeks,
            AverageSessionsPerWeekInRecentFourWeeks: >= ProfileTrainingExperiencePlanningRule.StrongBeginnerOverrideAverageSessionsPerWeek
        };

    private static bool IsStrongHistoryForAdvancedModality(ActivityTrainingHistory? history) =>
        history is
        {
            ConsistentParticipationWeeks: >= ProfileTrainingExperiencePlanningRule.StrongIntermediateToAdvancedConsistentWeeks,
            AverageSessionsPerWeekInRecentFourWeeks: >= ProfileTrainingExperiencePlanningRule.StrongIntermediateToAdvancedAverageSessionsPerWeek
        };
}
