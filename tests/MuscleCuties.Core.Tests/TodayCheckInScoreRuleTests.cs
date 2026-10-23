using System.Globalization;
using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class TodayCheckInScoreRuleTests
{
    private const string UtcTimeZone = "UTC";
    private const string TorontoTimeZone = "America/Toronto";
    private const string SampleUtcTimestamp = "2026-10-23T12:00:00Z";
    private const string ApprovalReference = "synthetic-review-fixture";
    private const decimal WorstFavorableScaleValue = 1m;
    private const decimal BestFavorableScaleValue = 5m;
    private const decimal MidpointScaleValue = 3m;
    private const decimal FractionalScaleValue = 2.5m;
    private const decimal ExpectedMinimumScore = 0m;
    private const decimal ExpectedMaximumScore = 100m;
    private const decimal ExpectedMidpointScore = 50m;
    private const decimal ExpectedOneStepChange = 6.25m;
    private const decimal ExpectedLowBandScore = 43.75m;
    private const decimal ExpectedModerateBandScore = 68.75m;
    private const decimal ExpectedHighBandScore = 75m;
    private const int PreviousDayOffset = -1;

    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse(
        SampleUtcTimestamp, CultureInfo.InvariantCulture);
    private static readonly DateOnly TodayUtc = DateOnly.FromDateTime(NowUtc.UtcDateTime);

    [Fact]
    public void Prototype_Endpoints_And_Midpoint_Match_Specified_Arithmetic()
    {
        Assert.Equal(ExpectedMinimumScore, TodayCheckInScoreRule.CalculatePrototype(
            WorstFavorableScaleValue, WorstFavorableScaleValue,
            BestFavorableScaleValue, BestFavorableScaleValue));
        Assert.Equal(ExpectedMaximumScore, TodayCheckInScoreRule.CalculatePrototype(
            BestFavorableScaleValue, BestFavorableScaleValue,
            WorstFavorableScaleValue, WorstFavorableScaleValue));
        Assert.Equal(ExpectedMidpointScore, TodayCheckInScoreRule.CalculatePrototype(
            MidpointScaleValue, MidpointScaleValue, MidpointScaleValue, MidpointScaleValue));
    }

    [Fact]
    public void One_Scale_Step_Changes_Raw_Score_By_Expected_Amount()
    {
        const decimal nextFavorableStep = 4m;
        const decimal nextSorenessStep = 4m;
        var midpoint = TodayCheckInScoreRule.CalculatePrototype(
            MidpointScaleValue, MidpointScaleValue, MidpointScaleValue, MidpointScaleValue);

        Assert.Equal(ExpectedOneStepChange,
            TodayCheckInScoreRule.CalculatePrototype(nextFavorableStep, MidpointScaleValue,
                MidpointScaleValue, MidpointScaleValue) - midpoint);
        Assert.Equal(-ExpectedOneStepChange,
            TodayCheckInScoreRule.CalculatePrototype(MidpointScaleValue, MidpointScaleValue,
                nextSorenessStep, MidpointScaleValue) - midpoint);
    }

    [Fact]
    public void Raw_Boundaries_Select_Bands_Before_Any_Display_Rounding()
    {
        var low = Evaluate(5m, 4m, 5m, 5m);
        var midpoint = Evaluate(MidpointScaleValue, MidpointScaleValue,
            MidpointScaleValue, MidpointScaleValue);
        var moderate = Evaluate(5m, 4m, 3m, 3m);
        var high = Evaluate(5m, 5m, 3m, 3m);

        Assert.Equal((ExpectedLowBandScore, CheckInScoreBand.ConsiderEasierDay),
            (low.RawScore, low.Band));
        Assert.Equal((ExpectedMidpointScore, CheckInScoreBand.ConsiderLighterSession),
            (midpoint.RawScore, midpoint.Band));
        Assert.Equal((ExpectedModerateBandScore, CheckInScoreBand.ConsiderLighterSession),
            (moderate.RawScore, moderate.Band));
        Assert.Equal((ExpectedHighBandScore, CheckInScoreBand.FeelingReady),
            (high.RawScore, high.Band));
    }

    [Fact]
    public void Missing_Or_Fractional_Scale_Produces_No_Score()
    {
        var incomplete = TodayCheckInScoreRule.Evaluate(
            Observation(sleep: null), NowUtc, UtcTimeZone, true, Approved());
        var fractional = TodayCheckInScoreRule.Evaluate(
            Observation(sleep: FractionalScaleValue), NowUtc, UtcTimeZone, true, Approved());

        Assert.Equal(CheckInScoreStatus.Incomplete, incomplete.Status);
        Assert.Null(incomplete.RawScore);
        Assert.Equal(CheckInScoreStatus.InvalidInput, fractional.Status);
        Assert.Null(fractional.RawScore);
    }

    [Fact]
    public void Yesterday_And_Changed_TimeZone_Do_Not_Reassign_Observation()
    {
        var yesterday = TodayCheckInScoreRule.Evaluate(
            Observation(date: TodayUtc.AddDays(PreviousDayOffset)), NowUtc, UtcTimeZone, true, Approved());
        var changedZone = TodayCheckInScoreRule.Evaluate(
            Observation(), NowUtc, TorontoTimeZone, true, Approved());

        Assert.Equal(CheckInScoreStatus.Stale, yesterday.Status);
        Assert.Null(yesterday.RawScore);
        Assert.Equal(CheckInScoreStatus.WrongTimeZoneAssociation, changedZone.Status);
        Assert.Null(changedZone.RawScore);
    }

    [Fact]
    public void Display_Requires_Both_Flag_And_Section_Approval()
    {
        var disabled = TodayCheckInScoreRule.Evaluate(
            Observation(), NowUtc, UtcTimeZone, false, Approved());
        var unapproved = TodayCheckInScoreRule.Evaluate(
            Observation(), NowUtc, UtcTimeZone, true, null);
        var wrongSection = TodayCheckInScoreRule.Evaluate(
            Observation(), NowUtc, UtcTimeZone, true,
            Approved(PlanningReviewSection.TrainingPrescriptions));

        Assert.Equal(CheckInScoreStatus.FeatureDisabled, disabled.Status);
        Assert.Equal(CheckInScoreStatus.NeedsProfessionalReview, unapproved.Status);
        Assert.Equal(CheckInScoreStatus.NeedsProfessionalReview, wrongSection.Status);
        Assert.Null(disabled.RawScore);
        Assert.Null(unapproved.RawScore);
        Assert.Null(wrongSection.RawScore);
    }

    private static CheckInScoreResult Evaluate(decimal sleep, decimal energy, decimal soreness, decimal stress) =>
        TodayCheckInScoreRule.Evaluate(
            Observation(sleep: sleep, energy: energy, soreness: soreness, stress: stress),
            NowUtc, UtcTimeZone, true, Approved());

    private static TodayCheckInObservation Observation(
        DateOnly? date = null, decimal? sleep = MidpointScaleValue,
        decimal? energy = MidpointScaleValue, decimal? soreness = MidpointScaleValue,
        decimal? stress = MidpointScaleValue) =>
        new(date ?? TodayUtc, UtcTimeZone, NowUtc, 1, sleep, energy, soreness, stress);

    private static PlanningReviewApproval Approved(
        PlanningReviewSection section = PlanningReviewSection.ReadinessScoreDisplay) =>
        new(TodayCheckInScoreRule.RulesetVersion, ApprovalReference, NowUtc,
            new HashSet<PlanningReviewSection> { section });
}
