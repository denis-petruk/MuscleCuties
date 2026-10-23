using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class TodayPlanningDecisionRuleTests
{
    private const decimal MaximumPrototypeScore = 100m;
    private const decimal ModeratePrototypeScore = 50m;
    private const string ApprovalReference = "synthetic-suggestion-approval";
    private static readonly DateTimeOffset ApprovedAtUtc = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Current_Symptoms_Win_Over_Maximum_Score()
    {
        var decision = TodayPlanningDecisionRule.Evaluate(Context(
            Score(MaximumPrototypeScore, CheckInScoreBand.FeelingReady),
            currentSymptoms: true));

        Assert.Equal(TodayPlanningStatus.SymptomReviewRequired, decision.Status);
        Assert.Contains(TodayPlanningReason.CurrentSymptomsRequireReviewedPathway, decision.Reasons);
    }

    [Fact]
    public void Explicit_Restriction_Wins_Over_Score_And_Suggestion_Approval()
    {
        var decision = TodayPlanningDecisionRule.Evaluate(Context(
            Score(MaximumPrototypeScore, CheckInScoreBand.FeelingReady),
            restrictionConflict: true, approval: Approval()));

        Assert.Equal(TodayPlanningStatus.RestrictionBlocked, decision.Status);
    }

    [Fact]
    public void Incomplete_CheckIn_Shows_Baseline_Without_Score_Advice()
    {
        var incomplete = new CheckInScoreResult(CheckInScoreStatus.Incomplete, null, null, null);

        var decision = TodayPlanningDecisionRule.Evaluate(Context(incomplete));

        Assert.Equal(TodayPlanningStatus.BaselineVisible, decision.Status);
        Assert.Contains(TodayPlanningReason.CheckInUnavailable, decision.Reasons);
    }

    [Fact]
    public void High_Score_Retains_Current_Plan_Without_Increase()
    {
        var decision = TodayPlanningDecisionRule.Evaluate(Context(
            Score(MaximumPrototypeScore, CheckInScoreBand.FeelingReady), approval: Approval()));

        Assert.Equal(TodayPlanningStatus.RetainCurrentPlan, decision.Status);
        Assert.Contains(TodayPlanningReason.HighScoreDoesNotIncreasePlan, decision.Reasons);
    }

    [Fact]
    public void Moderate_Score_Needs_Separate_Suggestion_Approval()
    {
        var score = Score(ModeratePrototypeScore, CheckInScoreBand.ConsiderLighterSession);
        var unavailable = TodayPlanningDecisionRule.Evaluate(Context(score));
        var approved = TodayPlanningDecisionRule.Evaluate(Context(score, approval: Approval()));

        Assert.Equal(TodayPlanningStatus.BaselineVisible, unavailable.Status);
        Assert.Contains(TodayPlanningReason.SuggestionsNeedProfessionalReview, unavailable.Reasons);
        Assert.Equal(TodayPlanningStatus.OptionalWorkReviewAvailable, approved.Status);
    }

    private static CheckInScoreResult Score(decimal raw, CheckInScoreBand band) =>
        new(CheckInScoreStatus.Available, raw, band, null);

    private static TodayPlanningContext Context(CheckInScoreResult score,
        bool currentSymptoms = false, bool restrictionConflict = false,
        PlanningReviewApproval? approval = null) =>
        new(currentSymptoms, false, restrictionConflict, score, true, approval);

    private static PlanningReviewApproval Approval() =>
        new(TodayCheckInScoreRule.RulesetVersion, ApprovalReference, ApprovedAtUtc,
            new HashSet<PlanningReviewSection> { PlanningReviewSection.ReadinessSuggestions });
}
