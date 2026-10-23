using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum WorkoutAdjustmentKindV020
{
    None,
    RepeatCurrentPrescription,
    IncreaseLoad,
    DecreaseLoad,
    ReduceCardioDuration,
    IncreaseCardioDuration,
    RequestManualAdjustment,
    SymptomReviewRequired,
    LighterWeek
}

public sealed record StrengthPerformanceEvidenceV020(
    Guid SessionId,
    int ExerciseId,
    DateTimeOffset CompletedAtUtc,
    bool IsComplete,
    bool EverySetReachedTopOfRange,
    bool TargetEffortMet,
    bool IsBelowLowerRepBoundary,
    bool HasRelevantPainOrLimitation,
    decimal? CurrentLoad,
    IReadOnlyList<decimal> AvailableLoadIncrements);

public sealed record CardioPerformanceEvidenceV020(
    Guid SessionId,
    DateTimeOffset CompletedAtUtc,
    int PrescribedMainMinutes,
    bool CompletedAtCurrentDuration,
    bool ReportedEasierOrAsPlanned,
    bool StoppedEarlyFromOrdinaryExertion,
    bool HasRelevantSymptoms);

public sealed record WorkoutAdjustmentProposalV020(
    WorkoutAdjustmentKindV020 Kind,
    WorkoutActivityType Activity,
    int? ExerciseId,
    decimal? ProposedLoad,
    int? ProposedRepetitions,
    int? ProposedMainMinutes,
    int? SetCountReduction,
    int? SessionCountReduction,
    bool RequiresUserConfirmation,
    IReadOnlyList<string> Reasons);

/// <summary>Proposes reviewed performance adjustments. Nothing is applied until the user confirms.</summary>
public static class WorkoutProgressionRuleV020
{
    private const int NoChange = 0;

    public static WorkoutAdjustmentProposalV020 EvaluateStrength(
        WorkoutActivityType activity,
        int exerciseId,
        int lowerRepBoundary,
        int currentRepTarget,
        IReadOnlyList<StrengthPerformanceEvidenceV020>? history,
        bool hasCurrentPainOrLimitation)
    {
        if (hasCurrentPainOrLimitation || history?.Any(item => item.HasRelevantPainOrLimitation) == true)
            return Proposal(WorkoutAdjustmentKindV020.SymptomReviewRequired, activity,
                exerciseId, "PainOrConcerningSymptomsUseSeparateReviewPathway");
        if (history is null || history.Count == 0 || exerciseId <= 0 || lowerRepBoundary <= 0 ||
            currentRepTarget < lowerRepBoundary || history.Any(item => item is null || item.SessionId == Guid.Empty ||
                item.ExerciseId != exerciseId || item.CompletedAtUtc.Offset != TimeSpan.Zero ||
                item.AvailableLoadIncrements is null || item.AvailableLoadIncrements.Any(increment => increment <= 0)) ||
            history.Select(item => item.SessionId).Distinct().Count() != history.Count)
            return Proposal(WorkoutAdjustmentKindV020.None, activity, exerciseId, "InsufficientOrInvalidPerformanceEvidence");

        var ordered = history.OrderByDescending(item => item.CompletedAtUtc).ToArray();
        var qualifyingProgressions = ordered.TakeWhile(item => item.IsComplete &&
            item.EverySetReachedTopOfRange && item.TargetEffortMet).ToArray();
        if (qualifyingProgressions.Length >= WorkoutTrainingPolicyV020.StrengthProgressionQualifyingExposures)
        {
            var currentLoad = ordered[0].CurrentLoad;
            if (currentLoad is null or <= 0)
                return Proposal(WorkoutAdjustmentKindV020.None, activity, exerciseId,
                    "LoadUnknownRetainCurrentPrescription");
            var increment = ordered[0].AvailableLoadIncrements.Where(value => value > 0)
                .OrderBy(value => value).FirstOrDefault();
            return increment > 0
                ? new WorkoutAdjustmentProposalV020(WorkoutAdjustmentKindV020.IncreaseLoad, activity,
                    exerciseId, currentLoad + increment, lowerRepBoundary, null, null, null, true,
                    ["TwoConsecutiveQualifyingExposures", "SmallestAvailableLoadIncrement", "ResetRepsToLowerBoundary"])
                : Proposal(WorkoutAdjustmentKindV020.None, activity, exerciseId,
                    "NoPracticalLoadIncrementRetainCurrentLoad");
        }

        var belowRange = ordered.TakeWhile(item => item.IsComplete && item.IsBelowLowerRepBoundary).ToArray();
        if (belowRange.Length >= WorkoutTrainingPolicyV020.StrengthRegressionBelowRangeExposures)
        {
            var currentLoad = ordered[0].CurrentLoad;
            if (currentLoad is null or <= 0)
                return Proposal(WorkoutAdjustmentKindV020.RequestManualAdjustment, activity, exerciseId,
                    "LoadUnknownRequestManualAdjustment");
            var decrement = ordered[0].AvailableLoadIncrements.Where(value => value > 0 && value < currentLoad)
                .OrderBy(value => value).FirstOrDefault();
            return decrement > 0
                ? new WorkoutAdjustmentProposalV020(WorkoutAdjustmentKindV020.DecreaseLoad, activity,
                    exerciseId, currentLoad - decrement, lowerRepBoundary, null, null, null, true,
                    ["TwoConsecutiveExposuresBelowLowerRepBoundary", "OneAvailableLoadIncrementDown"])
                : Proposal(WorkoutAdjustmentKindV020.RequestManualAdjustment, activity, exerciseId,
                    "NoPracticalLoadDecrementAvailable");
        }

        return Proposal(WorkoutAdjustmentKindV020.RepeatCurrentPrescription, activity, exerciseId,
            ordered[0].IsComplete ? "NoProgressionThresholdMet" : "IncompleteExposureRepeatWithoutIncrease");
    }

    public static WorkoutAdjustmentProposalV020 EvaluateCardio(
        WorkoutActivityType activity,
        IReadOnlyList<CardioPerformanceEvidenceV020>? history,
        int currentMainBlockMinutes,
        bool hasCurrentSymptoms)
    {
        if (hasCurrentSymptoms || history?.Any(item => item.HasRelevantSymptoms) == true)
            return Proposal(WorkoutAdjustmentKindV020.SymptomReviewRequired, activity,
                reason: "PainOrConcerningSymptomsUseSeparateReviewPathway");
        if (history is null || history.Count == 0 || currentMainBlockMinutes <
            WorkoutTrainingPolicyV020.RegressionCardioMinimumMainMinutes || history.Any(item => item is null ||
                item.SessionId == Guid.Empty || item.CompletedAtUtc.Offset != TimeSpan.Zero ||
                item.PrescribedMainMinutes <= 0) ||
            history.Select(item => item.SessionId).Distinct().Count() != history.Count)
            return Proposal(WorkoutAdjustmentKindV020.None, activity, reason: "InsufficientOrInvalidCardioEvidence");

        var ordered = history.OrderByDescending(item => item.CompletedAtUtc).ToArray();
        var progressionCount = ordered.TakeWhile(item => item.CompletedAtCurrentDuration &&
            item.PrescribedMainMinutes == currentMainBlockMinutes && item.ReportedEasierOrAsPlanned &&
            !item.StoppedEarlyFromOrdinaryExertion).Count();
        if (progressionCount >= WorkoutTrainingPolicyV020.CardioProgressionQualifyingSessions)
            return new WorkoutAdjustmentProposalV020(WorkoutAdjustmentKindV020.IncreaseCardioDuration,
                activity, null, null, null,
                checked(currentMainBlockMinutes + WorkoutTrainingPolicyV020.CardioProgressionMinutes),
                null, null, true, ["TwoCompletedManageableSessionsAtCurrentDuration", "IntensityUnchanged"]);

        var earlyStops = ordered.TakeWhile(item => item.PrescribedMainMinutes == currentMainBlockMinutes &&
            item.StoppedEarlyFromOrdinaryExertion && !item.HasRelevantSymptoms).Count();
        if (earlyStops >= WorkoutTrainingPolicyV020.CardioRegressionEarlyStops)
        {
            var reducedMinutes = Math.Max(WorkoutTrainingPolicyV020.RegressionCardioMinimumMainMinutes,
                currentMainBlockMinutes - WorkoutTrainingPolicyV020.CardioRegressionMinutes);
            return new WorkoutAdjustmentProposalV020(WorkoutAdjustmentKindV020.ReduceCardioDuration,
                activity, null, null, null, reducedMinutes, null, null, true,
                ["TwoSessionsStoppedEarlyFromOrdinaryExertion", "IntensityUnchanged"]);
        }

        return Proposal(WorkoutAdjustmentKindV020.None, activity, reason: "NoCardioAdjustmentThresholdMet");
    }

    public static WorkoutAdjustmentProposalV020 ProposeLighterWeek(
        WorkoutActivityType activity,
        bool userSelected,
        bool hasOptionalCardioSession,
        int? cardioMainMinutes = null)
    {
        if (!userSelected)
            return Proposal(WorkoutAdjustmentKindV020.None, activity, reason: "LighterWeekNotSelected");

        if (activity is WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.StrengthHighIntensity)
            return new WorkoutAdjustmentProposalV020(WorkoutAdjustmentKindV020.LighterWeek,
                activity, null, null, null, null, WorkoutTrainingPolicyV020.LighterWeekSetReduction,
                null, true, ["OneFewerSetPerExerciseMinimumOne", "NoHIITOrProgression"]);

        if (activity is WorkoutActivityType.Hiit or WorkoutActivityType.Cycling or WorkoutActivityType.Running or
            WorkoutActivityType.Swimming or WorkoutActivityType.Walking or WorkoutActivityType.Rowing or
            WorkoutActivityType.Elliptical)
        {
            if (hasOptionalCardioSession)
                return new WorkoutAdjustmentProposalV020(WorkoutAdjustmentKindV020.LighterWeek,
                    activity, null, null, null, null, null, 1, true,
                    ["RemoveOneOptionalCardioSession", "NoAutomaticRestoration"]);
            if (cardioMainMinutes is { } currentMinutes && currentMinutes > 0)
                return new WorkoutAdjustmentProposalV020(WorkoutAdjustmentKindV020.LighterWeek,
                    activity, null, null, null,
                    Math.Max(WorkoutTrainingPolicyV020.RegressionCardioMinimumMainMinutes,
                        currentMinutes - WorkoutTrainingPolicyV020.LighterWeekCardioReductionMinutes),
                    null, null, true, ["ShortenOneMainBlockByFiveMinutes", "NoAutomaticRestoration"]);
        }

        return Proposal(WorkoutAdjustmentKindV020.None, activity,
            reason: "NoEligibleLighterWeekAdjustmentAvailable");
    }

    private static WorkoutAdjustmentProposalV020 Proposal(WorkoutAdjustmentKindV020 kind,
        WorkoutActivityType activity, string reason, int? exerciseId = null) =>
        new(kind, activity, exerciseId, null, null, null, null, null,
            kind is WorkoutAdjustmentKindV020.RequestManualAdjustment,
            [reason]);

    private static WorkoutAdjustmentProposalV020 Proposal(WorkoutAdjustmentKindV020 kind,
        WorkoutActivityType activity, int? exerciseId, string reason) =>
        Proposal(kind, activity, reason, exerciseId);
}
