namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum TodayPlanningStatus
{
    RestrictionBlocked,
    SymptomReviewRequired,
    BaselineVisible,
    RetainCurrentPlan,
    OptionalWorkReviewAvailable,
    EasierDayOptionsAvailable
}

public enum TodayPlanningReason
{
    ExplicitRestrictionConflict,
    CurrentSymptomsRequireReviewedPathway,
    CheckInUnavailable,
    HighScoreDoesNotIncreasePlan,
    OptionalWorkMayBeReviewed,
    ConsiderEasierDay
}

public sealed record TodayPlanningContext(
    bool HasCurrentSessionSymptoms,
    bool HasDailySymptoms,
    bool HasExplicitRestrictionConflict,
    CheckInScoreResult? CheckInScore);

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
        return band switch
        {
            CheckInScoreBand.FeelingReady => new TodayPlanningDecision(
                TodayPlanningStatus.RetainCurrentPlan,
                [TodayPlanningReason.HighScoreDoesNotIncreasePlan]),
            CheckInScoreBand.ConsiderLighterSession => new TodayPlanningDecision(
                TodayPlanningStatus.OptionalWorkReviewAvailable,
                [TodayPlanningReason.OptionalWorkMayBeReviewed]),
            CheckInScoreBand.ConsiderEasierDay => new TodayPlanningDecision(
                TodayPlanningStatus.EasierDayOptionsAvailable,
                [TodayPlanningReason.ConsiderEasierDay]),
            _ => new TodayPlanningDecision(TodayPlanningStatus.BaselineVisible,
                [TodayPlanningReason.CheckInUnavailable])
        };
    }
}
