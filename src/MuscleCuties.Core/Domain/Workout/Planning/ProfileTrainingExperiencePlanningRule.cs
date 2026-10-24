using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record ProfileTrainingExperienceDecision(
    ActivityExperience ActivityHistoryExperience,
    ActivityExperience PrescriptionExperience,
    bool UsesConservativeProfileCap,
    IReadOnlyList<string> ReasonCodes,
    TrainingExperienceLevel EffectiveExperienceLevel = TrainingExperienceLevel.Beginner,
    int DefaultResistanceDaysPerWeek = ProfileTrainingExperiencePlanningRule.BeginnerDefaultResistanceDaysPerWeek,
    int MaximumResistanceDaysPerWeek = ProfileTrainingExperiencePlanningRule.BeginnerMaximumResistanceDaysPerWeek,
    int MinimumSetsPerExercise = ProfileTrainingExperiencePlanningRule.BeginnerMinimumSetsPerExercise,
    int MaximumSetsPerExercise = ProfileTrainingExperiencePlanningRule.BeginnerMaximumSetsPerExercise,
    int MinimumRepetitions = ProfileTrainingExperiencePlanningRule.BeginnerMinimumRepetitions,
    int MaximumRepetitions = ProfileTrainingExperiencePlanningRule.BeginnerMaximumRepetitions,
    int MinimumWeeklySetsPerMuscle = ProfileTrainingExperiencePlanningRule.BeginnerMinimumWeeklySetsPerMuscle,
    int MaximumWeeklySetsPerMuscle = ProfileTrainingExperiencePlanningRule.BeginnerMaximumWeeklySetsPerMuscle,
    int MaxConcurrentProgressionKnobs = ProfileTrainingExperiencePlanningRule.StandardMaxConcurrentProgressionKnobs,
    bool AllowsAggressivePeriodization = false);

/// <summary>
/// Combines activity-specific training history (ground truth for current modality tolerance)
/// with the profile-level <see cref="TrainingExperienceLevel"/> (global training-age prior,
/// fallback when history is sparse, and ceiling for volume, frequency, and progression speed).
/// </summary>
public static class ProfileTrainingExperiencePlanningRule
{
    public const string BeginnerProfileCapReason = "BeginnerProfileUsesConservativeStrengthPrescription";
    public const string NearZeroRecentExposureDowngradeReason = "NearZeroRecentExposureDowngradedToBeginner";
    public const string StrongHistoryUpgradesBeginnerProfileReason =
        "StrongActivityHistoryUpgradesBeginnerProfileWithVolumeCeiling";
    public const string StrongHistoryUpgradesIntermediateModalityReason =
        "StrongActivityHistoryUpgradesModalityWithProfileVolumeCap";
    public const string ProfileExperienceFallbackReason = "ProfileExperienceUsedAsFallbackForMissingHistory";

    public const int StrongBeginnerOverrideConsistentWeeks = 24;
    public const decimal StrongBeginnerOverrideAverageSessionsPerWeek = 3m;
    public const int StrongIntermediateToAdvancedConsistentWeeks = 80;
    public const decimal StrongIntermediateToAdvancedAverageSessionsPerWeek = 3m;
    public const decimal MinimumActiveAverageSessionsPerWeek = 1m;
    private const int ZeroConsistentWeeks = 0;

    public const int BeginnerDefaultResistanceDaysPerWeek = 2;
    public const int BeginnerMaximumResistanceDaysPerWeek = 3;
    public const int BeginnerMinimumSetsPerExercise = 1;
    public const int BeginnerMaximumSetsPerExercise = 3;
    public const int BeginnerMinimumRepetitions = 8;
    public const int BeginnerMaximumRepetitions = 12;
    public const int BeginnerMinimumWeeklySetsPerMuscle = 8;
    public const int BeginnerMaximumWeeklySetsPerMuscle = 12;

    public const int IntermediateDefaultResistanceDaysPerWeek = 3;
    public const int IntermediateMaximumResistanceDaysPerWeek = 4;
    public const int IntermediateMinimumSetsPerExercise = 2;
    public const int IntermediateMaximumSetsPerExercise = 4;
    public const int IntermediateMinimumRepetitions = 6;
    public const int IntermediateMaximumRepetitions = 12;
    public const int IntermediateMinimumWeeklySetsPerMuscle = 12;
    public const int IntermediateMaximumWeeklySetsPerMuscle = 18;

    public const int AdvancedDefaultResistanceDaysPerWeek = 4;
    public const int AdvancedMaximumResistanceDaysPerWeek = 5;
    public const int AdvancedMinimumSetsPerExercise = 3;
    public const int AdvancedMaximumSetsPerExercise = 5;
    public const int AdvancedMinimumRepetitions = 1;
    public const int AdvancedMaximumRepetitions = 12;
    public const int AdvancedMinimumWeeklySetsPerMuscle = 16;
    public const int AdvancedMaximumWeeklySetsPerMuscle = 22;

    public const int StandardMaxConcurrentProgressionKnobs = 1;
    public const int AdvancedMaxConcurrentProgressionKnobs = 2;

    public static ProfileTrainingExperienceDecision Resolve(
        ActivityExperience activityHistoryExperience,
        TrainingExperienceLevel? profileExperience,
        ActivityTrainingHistory? history = null,
        bool allowProfileFallbackWhenHistoryMissing = false)
    {
        if (!Enum.IsDefined(activityHistoryExperience))
            return BuildDecision(
                ActivityExperience.Unknown,
                ActivityExperience.Unknown,
                false,
                ["ActivityExperienceInvalid"],
                TrainingExperienceLevel.Unknown);

        var normalizedProfile = NormalizeProfileLevel(profileExperience);
        var hasNearZeroRecentExposure = HasNearZeroRecentExposure(history);

        if (activityHistoryExperience == ActivityExperience.Unknown)
        {
            if (hasNearZeroRecentExposure)
                return BuildDecision(
                    ActivityExperience.Unknown,
                    allowProfileFallbackWhenHistoryMissing ? ActivityExperience.Beginner : ActivityExperience.Unknown,
                    false,
                    [NearZeroRecentExposureDowngradeReason],
                    TrainingExperienceLevel.Beginner);

            if (allowProfileFallbackWhenHistoryMissing && normalizedProfile != TrainingExperienceLevel.Unknown)
            {
                var fallbackPrescription = normalizedProfile is TrainingExperienceLevel.Intermediate or
                    TrainingExperienceLevel.Advanced
                    ? ActivityExperience.Established
                    : ActivityExperience.Beginner;
                return BuildDecision(
                    ActivityExperience.Unknown,
                    fallbackPrescription,
                    false,
                    [ProfileExperienceFallbackReason],
                    normalizedProfile);
            }

            return BuildDecision(
                ActivityExperience.Unknown,
                ActivityExperience.Unknown,
                false,
                [],
                normalizedProfile);
        }

        if (activityHistoryExperience == ActivityExperience.Returning)
        {
            return BuildDecision(
                ActivityExperience.Returning,
                ActivityExperience.Returning,
                false,
                [],
                TrainingExperienceLevel.Beginner);
        }

        if (activityHistoryExperience == ActivityExperience.Beginner)
        {
            if (hasNearZeroRecentExposure && normalizedProfile is TrainingExperienceLevel.Intermediate or
                TrainingExperienceLevel.Advanced)
            {
                return BuildDecision(
                    ActivityExperience.Beginner,
                    ActivityExperience.Beginner,
                    false,
                    [NearZeroRecentExposureDowngradeReason],
                    TrainingExperienceLevel.Beginner);
            }

            return BuildDecision(
                ActivityExperience.Beginner,
                ActivityExperience.Beginner,
                false,
                [],
                TrainingExperienceLevel.Beginner);
        }

        // activityHistoryExperience == ActivityExperience.Established
        if (normalizedProfile == TrainingExperienceLevel.Beginner)
        {
            if (IsStrongHistoryForBeginnerUpgrade(history))
            {
                return BuildDecision(
                    ActivityExperience.Established,
                    ActivityExperience.Established,
                    false,
                    [StrongHistoryUpgradesBeginnerProfileReason],
                    TrainingExperienceLevel.Intermediate,
                    volumeCapOverride: BeginnerMaximumWeeklySetsPerMuscle);
            }

            return BuildDecision(
                ActivityExperience.Established,
                ActivityExperience.Beginner,
                true,
                [BeginnerProfileCapReason],
                TrainingExperienceLevel.Beginner);
        }

        if (normalizedProfile == TrainingExperienceLevel.Intermediate)
        {
            if (IsStrongHistoryForAdvancedModality(history))
            {
                return BuildDecision(
                    ActivityExperience.Established,
                    ActivityExperience.Established,
                    false,
                    [StrongHistoryUpgradesIntermediateModalityReason],
                    TrainingExperienceLevel.Advanced,
                    volumeCapOverride: IntermediateMaximumWeeklySetsPerMuscle);
            }

            return BuildDecision(
                ActivityExperience.Established,
                ActivityExperience.Established,
                false,
                [],
                TrainingExperienceLevel.Intermediate);
        }

        if (normalizedProfile == TrainingExperienceLevel.Advanced)
        {
            return BuildDecision(
                ActivityExperience.Established,
                ActivityExperience.Established,
                false,
                [],
                TrainingExperienceLevel.Advanced);
        }

        return BuildDecision(
            ActivityExperience.Established,
            ActivityExperience.Established,
            false,
            [],
            TrainingExperienceLevel.Intermediate);
    }

    private static TrainingExperienceLevel NormalizeProfileLevel(TrainingExperienceLevel? profileExperience) =>
        profileExperience is { } level && Enum.IsDefined(level)
            ? level
            : TrainingExperienceLevel.Unknown;

    private static bool HasNearZeroRecentExposure(ActivityTrainingHistory? history) =>
        history is not null &&
        (history.ConsistentParticipationWeeks is <= ZeroConsistentWeeks ||
         history.AverageSessionsPerWeekInRecentFourWeeks is < MinimumActiveAverageSessionsPerWeek);

    private static bool IsStrongHistoryForBeginnerUpgrade(ActivityTrainingHistory? history) =>
        history is
        {
            ConsistentParticipationWeeks: >= StrongBeginnerOverrideConsistentWeeks,
            AverageSessionsPerWeekInRecentFourWeeks: >= StrongBeginnerOverrideAverageSessionsPerWeek
        };

    private static bool IsStrongHistoryForAdvancedModality(ActivityTrainingHistory? history) =>
        history is
        {
            ConsistentParticipationWeeks: >= StrongIntermediateToAdvancedConsistentWeeks,
            AverageSessionsPerWeekInRecentFourWeeks: >= StrongIntermediateToAdvancedAverageSessionsPerWeek
        };

    private static ProfileTrainingExperienceDecision BuildDecision(
        ActivityExperience historyExperience,
        ActivityExperience prescriptionExperience,
        bool usesConservativeProfileCap,
        IReadOnlyList<string> reasonCodes,
        TrainingExperienceLevel effectiveLevel,
        int? volumeCapOverride = null)
    {
        return effectiveLevel switch
        {
            TrainingExperienceLevel.Advanced => new ProfileTrainingExperienceDecision(
                historyExperience,
                prescriptionExperience,
                usesConservativeProfileCap,
                reasonCodes,
                TrainingExperienceLevel.Advanced,
                AdvancedDefaultResistanceDaysPerWeek,
                AdvancedMaximumResistanceDaysPerWeek,
                AdvancedMinimumSetsPerExercise,
                AdvancedMaximumSetsPerExercise,
                AdvancedMinimumRepetitions,
                AdvancedMaximumRepetitions,
                AdvancedMinimumWeeklySetsPerMuscle,
                volumeCapOverride ?? AdvancedMaximumWeeklySetsPerMuscle,
                AdvancedMaxConcurrentProgressionKnobs,
                AllowsAggressivePeriodization: true),
            TrainingExperienceLevel.Intermediate => new ProfileTrainingExperienceDecision(
                historyExperience,
                prescriptionExperience,
                usesConservativeProfileCap,
                reasonCodes,
                TrainingExperienceLevel.Intermediate,
                IntermediateDefaultResistanceDaysPerWeek,
                IntermediateMaximumResistanceDaysPerWeek,
                IntermediateMinimumSetsPerExercise,
                IntermediateMaximumSetsPerExercise,
                IntermediateMinimumRepetitions,
                IntermediateMaximumRepetitions,
                IntermediateMinimumWeeklySetsPerMuscle,
                volumeCapOverride ?? IntermediateMaximumWeeklySetsPerMuscle,
                StandardMaxConcurrentProgressionKnobs,
                AllowsAggressivePeriodization: false),
            _ => new ProfileTrainingExperienceDecision(
                historyExperience,
                prescriptionExperience,
                usesConservativeProfileCap,
                reasonCodes,
                effectiveLevel == TrainingExperienceLevel.Unknown
                    ? TrainingExperienceLevel.Unknown
                    : TrainingExperienceLevel.Beginner,
                BeginnerDefaultResistanceDaysPerWeek,
                BeginnerMaximumResistanceDaysPerWeek,
                BeginnerMinimumSetsPerExercise,
                BeginnerMaximumSetsPerExercise,
                BeginnerMinimumRepetitions,
                BeginnerMaximumRepetitions,
                BeginnerMinimumWeeklySetsPerMuscle,
                volumeCapOverride ?? BeginnerMaximumWeeklySetsPerMuscle,
                StandardMaxConcurrentProgressionKnobs,
                AllowsAggressivePeriodization: false)
        };
    }
}
