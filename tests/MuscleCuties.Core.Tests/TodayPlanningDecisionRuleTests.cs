using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class TodayPlanningDecisionRuleTests
{
    private const decimal MaximumPrototypeScore = 100m;
    private const decimal ModeratePrototypeScore = 50m;

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
    public void Explicit_Restriction_Wins_Over_Score()
    {
        var decision = TodayPlanningDecisionRule.Evaluate(Context(
            Score(MaximumPrototypeScore, CheckInScoreBand.FeelingReady),
            restrictionConflict: true));

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
            Score(MaximumPrototypeScore, CheckInScoreBand.FeelingReady)));

        Assert.Equal(TodayPlanningStatus.RetainCurrentPlan, decision.Status);
        Assert.Contains(TodayPlanningReason.HighScoreDoesNotIncreasePlan, decision.Reasons);
    }

    [Fact]
    public void Moderate_Score_Offers_Optional_Work_Review()
    {
        var score = Score(ModeratePrototypeScore, CheckInScoreBand.ConsiderLighterSession);
        var decision = TodayPlanningDecisionRule.Evaluate(Context(score));

        Assert.Equal(TodayPlanningStatus.OptionalWorkReviewAvailable, decision.Status);
    }

    [Fact]
    public void Low_Score_Offers_Easier_Day_Options()
    {
        var score = Score(0m, CheckInScoreBand.ConsiderEasierDay);
        var decision = TodayPlanningDecisionRule.Evaluate(Context(score));

        Assert.Equal(TodayPlanningStatus.EasierDayOptionsAvailable, decision.Status);
        Assert.Contains(TodayPlanningReason.ConsiderEasierDay, decision.Reasons);
    }

    private static CheckInScoreResult Score(decimal raw, CheckInScoreBand band) =>
        new(CheckInScoreStatus.Available, raw, band, null);

    private static TodayPlanningContext Context(CheckInScoreResult score,
        bool currentSymptoms = false, bool restrictionConflict = false) =>
        new(currentSymptoms, false, restrictionConflict, score);
}
