using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record ActivityHistoryInput(
    string Activity,
    int? WeeksConsistent,
    decimal? AvgSessionsLast4Weeks,
    int? DaysSinceLastSession);

public enum ModalityExperienceClassification
{
    New,
    Established,
    Returning,
    BetweenNewAndEstablished,
    Unknown
}

public static class ActivityHistoryPolicyRule
{
    public const int MinimumEstablishedWeeks = 12;
    public const decimal MinimumEstablishedSessionsPerWeek = 2m;
    public const int MinimumReturningInactivityDays = 28;

    public const int NewModalityWeeksThreshold = 4;
    public const decimal NewModalitySessionsThreshold = 1m;

    public const int GlobalTierBeginner = 1;
    public const int GlobalTierIntermediate = 2;
    public const int GlobalTierAdvanced = 3;

    public const int ModalityTierNew = 1;
    public const int ModalityTierEstablished = 2;
    public const int ModalityTierReturning = 1;

    public const int MinimumNonNegative = 0;

    public const string ReasonEstablished = "ESTABLISHED_HISTORY";
    public const string ReasonReturning = "RETURNING_AFTER_BREAK";
    public const string ReasonNew = "NEW_MODALITY";
    public const string ReasonDowngradedToTier1 = "DOWNGRADED_TO_TIER_1_ZERO_OR_LOW_EXPOSURE";
    public const string ReasonResolvedByEstablished = "ESTABLISHED_MODALITY_EFFECTIVE_TIER_RESOLVED";
    public const string ReasonResolvedByGlobalTier = "RESOLVED_BY_GLOBAL_TIER";

    public const string CodeInvalidHistoryInput = "INVALID_HISTORY_INPUT";
    public const string CodeMissingHistoryInput = "UNKNOWN_HISTORY_MISSING_INPUT";
    public const string CodeReturningEffectiveTierUnresolved = "RETURNING_EFFECTIVE_TIER_UNRESOLVED";
    public const string CodeBetweenNewAndEstablishedUnresolved = "EXPERIENCE_BETWEEN_NEW_AND_ESTABLISHED_UNRESOLVED";

    public static bool IsValidHistory(ActivityHistoryInput? input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Activity))
            return false;

        return input.WeeksConsistent is >= MinimumNonNegative &&
               input.AvgSessionsLast4Weeks is >= MinimumNonNegative &&
               input.DaysSinceLastSession is >= MinimumNonNegative;
    }

    public static bool HasMissingRequiredFields(ActivityHistoryInput? input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Activity))
            return true;

        return input.WeeksConsistent is null ||
               input.AvgSessionsLast4Weeks is null ||
               input.DaysSinceLastSession is null;
    }

    public static bool IsEstablished(ActivityHistoryInput? input)
    {
        if (!IsValidHistory(input) || HasMissingRequiredFields(input))
            return false;

        return input!.WeeksConsistent >= MinimumEstablishedWeeks &&
               input.AvgSessionsLast4Weeks >= MinimumEstablishedSessionsPerWeek;
    }

    public static bool IsReturning(ActivityHistoryInput? input)
    {
        if (!IsEstablished(input))
            return false;

        return input!.DaysSinceLastSession >= MinimumReturningInactivityDays;
    }

    public static bool IsNew(ActivityHistoryInput? input)
    {
        if (!IsValidHistory(input) || HasMissingRequiredFields(input))
            return false;

        return input!.WeeksConsistent < NewModalityWeeksThreshold ||
               input.AvgSessionsLast4Weeks < NewModalitySessionsThreshold;
    }

    public static ModalityExperienceClassification ClassifyModality(ActivityHistoryInput? input)
    {
        if (!IsValidHistory(input) || HasMissingRequiredFields(input))
            return ModalityExperienceClassification.Unknown;

        if (IsReturning(input))
            return ModalityExperienceClassification.Returning;

        if (IsEstablished(input))
            return ModalityExperienceClassification.Established;

        if (IsNew(input))
            return ModalityExperienceClassification.New;

        return ModalityExperienceClassification.BetweenNewAndEstablished;
    }

    public static int ResolveGlobalTier(TrainingExperienceLevel? profileLevel) =>
        profileLevel switch
        {
            TrainingExperienceLevel.Intermediate => GlobalTierIntermediate,
            TrainingExperienceLevel.Advanced => GlobalTierAdvanced,
            _ => GlobalTierBeginner
        };

    public static PolicyEvaluationResult<EffectiveExperienceTier> ResolveEffectiveExperience(
        TrainingExperienceLevel? profileLevel,
        ActivityHistoryInput? history)
    {
        var globalTier = ResolveGlobalTier(profileLevel);

        if (history is not null &&
            ((history.WeeksConsistent is < MinimumNonNegative) ||
             (history.AvgSessionsLast4Weeks is < MinimumNonNegative) ||
             (history.DaysSinceLastSession is < MinimumNonNegative)))
        {
            return PolicyEvaluationResult.Blocked<EffectiveExperienceTier>(
                CodeInvalidHistoryInput,
                "Supplied activity history contains negative values.");
        }

        if (HasMissingRequiredFields(history))
        {
            // Unknown or new: Start with GlobalTier
            // If GlobalTier > 1 and valid supplied history shows WeeksConsistent == 0 OR AvgSessionsLast4Weeks < 1, downgrade to tier 1
            if (globalTier > GlobalTierBeginner && history is not null &&
                ((history.WeeksConsistent == MinimumNonNegative) ||
                 (history.AvgSessionsLast4Weeks is { } avg && avg < NewModalitySessionsThreshold)))
            {
                return PolicyEvaluationResult.Allowed(
                    EffectiveExperienceTier.Tier1BeginnerOrNew,
                    ReasonDowngradedToTier1,
                    "Global tier downgraded to Tier 1 due to zero weeks or <1 avg session/week.");
            }

            if (history is null || (history.WeeksConsistent is null && history.AvgSessionsLast4Weeks is null && history.DaysSinceLastSession is null))
            {
                return PolicyEvaluationResult.MissingRequiredInput<EffectiveExperienceTier>(
                    CodeMissingHistoryInput,
                    "Required activity history fields are missing.");
            }

            return PolicyEvaluationResult.MissingRequiredInput<EffectiveExperienceTier>(
                CodeMissingHistoryInput,
                "One or more required activity history fields are missing.");
        }

        var classification = ClassifyModality(history);

        switch (classification)
        {
            case ModalityExperienceClassification.Established:
                // Established and not returning: min(3, max(2, GlobalTier))
                var effectiveInt = Math.Min(GlobalTierAdvanced, Math.Max(GlobalTierIntermediate, globalTier));
                return PolicyEvaluationResult.Allowed(
                    (EffectiveExperienceTier)effectiveInt,
                    ReasonResolvedByEstablished,
                    $"Established modality resolved effective experience tier to {effectiveInt} via min(3, max(2, {globalTier})).");

            case ModalityExperienceClassification.New:
                // Unknown or new: Start with GlobalTier.
                // If GlobalTier > 1 and valid supplied history shows WeeksConsistent == 0 OR AvgSessionsLast4Weeks < 1, downgrade to tier 1.
                if (globalTier > GlobalTierBeginner &&
                    (history!.WeeksConsistent == MinimumNonNegative ||
                     history.AvgSessionsLast4Weeks < NewModalitySessionsThreshold))
                {
                    return PolicyEvaluationResult.Allowed(
                        EffectiveExperienceTier.Tier1BeginnerOrNew,
                        ReasonDowngradedToTier1,
                        "New modality downgraded GlobalTier to Tier 1 due to zero consistency or <1 session/week.");
                }

                return PolicyEvaluationResult.Allowed(
                    (EffectiveExperienceTier)globalTier,
                    ReasonResolvedByGlobalTier,
                    $"New modality retains GlobalTier {globalTier}.");

            case ModalityExperienceClassification.Returning:
                // Specification Ambiguity: Modality tier = 1, but effective experience is unspecified for Returning users.
                return PolicyEvaluationResult.UnresolvedPolicy<EffectiveExperienceTier>(
                    CodeReturningEffectiveTierUnresolved,
                    "Specification does not define the final effective experience tier for returning users.");

            case ModalityExperienceClassification.BetweenNewAndEstablished:
                // Specification Ambiguity: Experience between new (<4 wks or <1 session) and established (>=12 wks and >=2 sessions).
                return PolicyEvaluationResult.UnresolvedPolicy<EffectiveExperienceTier>(
                    CodeBetweenNewAndEstablishedUnresolved,
                    "Specification does not define the modality or effective experience tier for users between new and established thresholds.");

            default:
                return PolicyEvaluationResult.MissingRequiredInput<EffectiveExperienceTier>(
                    CodeMissingHistoryInput,
                    "Unable to classify modality experience due to incomplete input.");
        }
    }
}
