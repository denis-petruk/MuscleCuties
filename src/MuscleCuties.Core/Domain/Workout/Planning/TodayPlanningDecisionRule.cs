namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum TodayPlanningStatus
{
    RestrictionBlocked,
    SymptomReviewRequired,
    BaselineVisible,
    RetainCurrentPlan,
    OptionalWorkReviewAvailable
}

public enum TodayPlanningReason
{
    ExplicitRestrictionConflict,
    CurrentSymptomsRequireReviewedPathway,
    CheckInUnavailable,
    SuggestionsDisabled,
    SuggestionsNeedProfessionalReview,
    HighScoreDoesNotIncreasePlan,
    OptionalWorkMayBeReviewed
}

public sealed record TodayPlanningContext(
    bool HasCurrentSessionSymptoms,
    bool HasDailySymptoms,
    bool HasExplicitRestrictionConflict,
    CheckInScoreResult? CheckInScore,
    bool SuggestionsEnabled,
    PlanningReviewApproval? Approval);

public sealed record TodayPlanningDecision(
    TodayPlanningStatus Status,
    IReadOnlyList<TodayPlanningReason> Reasons);

/// <summary>
/// Applies precedence only. It does not interpret symptoms or produce a workload change.
/// An accepted revision changes only through the separate proposal and acceptance workflow.
/// </summary>
public static class TodayPlanningDecisionRule
{
    public static TodayPlanningDecision Evaluate(TodayPlanningContext? context)
    {
        if (context is null)
            return new TodayPlanningDecision(TodayPlanningStatus.SymptomReviewRequired,
                [TodayPlanningReason.CurrentSymptomsRequireReviewedPathway]);

        if (context.HasExplicitRestrictionConflict)
            return new TodayPlanningDecision(TodayPlanningStatus.RestrictionBlocked,
                [TodayPlanningReason.ExplicitRestrictionConflict]);
        if (context.HasCurrentSessionSymptoms || context.HasDailySymptoms)
            return new TodayPlanningDecision(TodayPlanningStatus.SymptomReviewRequired,
                [TodayPlanningReason.CurrentSymptomsRequireReviewedPathway]);

        if (context.CheckInScore is not { Status: CheckInScoreStatus.Available, Band: { } band })
            return new TodayPlanningDecision(TodayPlanningStatus.BaselineVisible,
                [TodayPlanningReason.CheckInUnavailable]);
        if (band == CheckInScoreBand.FeelingReady)
            return new TodayPlanningDecision(TodayPlanningStatus.RetainCurrentPlan,
                [TodayPlanningReason.HighScoreDoesNotIncreasePlan]);
        if (!context.SuggestionsEnabled)
            return new TodayPlanningDecision(TodayPlanningStatus.BaselineVisible,
                [TodayPlanningReason.SuggestionsDisabled]);
        if (!HasSuggestionApproval(context.Approval))
            return new TodayPlanningDecision(TodayPlanningStatus.BaselineVisible,
                [TodayPlanningReason.SuggestionsNeedProfessionalReview]);

        return new TodayPlanningDecision(TodayPlanningStatus.OptionalWorkReviewAvailable,
            [TodayPlanningReason.OptionalWorkMayBeReviewed]);
    }

    private static bool HasSuggestionApproval(PlanningReviewApproval? approval) =>
        approval is not null && approval.RulesetVersion == TodayCheckInScoreRule.RulesetVersion &&
        !string.IsNullOrWhiteSpace(approval.ApprovalReference) &&
        approval.ApprovedAtUtc.Offset == TimeSpan.Zero &&
        approval.CoveredSections?.Contains(PlanningReviewSection.ReadinessSuggestions) == true;
}
