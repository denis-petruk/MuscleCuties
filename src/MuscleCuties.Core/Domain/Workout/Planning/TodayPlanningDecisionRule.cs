namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Whether today can be planned, must be reviewed, or is a rest day.</summary>
public enum TodayPlanningStatus
{
    RestrictionBlocked,
    SymptomReviewRequired,
    BaselineVisible,
    RetainCurrentPlan,
    OptionalWorkReviewAvailable,
    EasierDayOptionsAvailable
}

/// <summary>The stable code behind that status, so the client can explain it.</summary>
public enum TodayPlanningReason
{
    ExplicitRestrictionConflict,
    CurrentSymptomsRequireReviewedPathway,
    CheckInUnavailable,
    HighScoreDoesNotIncreasePlan,
    OptionalWorkMayBeReviewed,
    ConsiderEasierDay
}

/// <summary>What the decision reads: readiness, cycle phase, injuries and the existing plan.</summary>
public sealed record TodayPlanningContext(
    bool HasCurrentSessionSymptoms,
    bool HasDailySymptoms,
    bool HasExplicitRestrictionConflict,
    CheckInScoreResult? CheckInScore);

/// <summary>The decision with its reason, which the session screen acts on.</summary>
public sealed record TodayPlanningDecision(
    TodayPlanningStatus Status,
    IReadOnlyList<TodayPlanningReason> Reasons);

/// <summary>
/// Applies precedence only. It does not interpret symptoms or produce a workload change.
/// An accepted revision changes only through the separate proposal and acceptance workflow.
/// </summary>
public static class TodayPlanningDecisionRule
{
    /// <summary>Whether readiness suggestions are shown; they stay off until the model is validated.</summary>
    public static bool ReadinessSuggestionsEnabled => false;

    /// <summary>Prefers safety over progress, so low readiness or a recorded injury yields review rather than a hard session.</summary>
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
