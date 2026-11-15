using System.Globalization;
using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class TodayCheckInScoreRuleTests
{
    private const string UtcTimeZone = "UTC";
    private const string TorontoTimeZone = "America/Toronto";
    private const string SampleUtcTimestamp = "2026-10-23T12:00:00Z";
    private const decimal WorstFavorableScaleValue = 1m;
    private const decimal BestFavorableScaleValue = 5m;
    private const decimal MidpointScaleValue = 3m;
    private const decimal FractionalScaleValue = 2.5m;
    private const decimal ExpectedMinimumScore = 0m;
    private const decimal ExpectedMaximumScore = 100m;
    private const decimal ExpectedMidpointScore = 50m;
    // Three scale shares (sleep quality, energy, stress) rescale onto the same 0–100 range.
    private const decimal ExpectedOneStepChange = TodayCheckInScoreRule.ReadinessScoreMaximum * 1m / 12m;
    private const decimal ExpectedLowBandScore = TodayCheckInScoreRule.ReadinessScoreMaximum * 4m / 12m;
    private const decimal ExpectedModerateBandScore = TodayCheckInScoreRule.ReadinessScoreMaximum * 8m / 12m;
    private const decimal ExpectedHighBandScore = TodayCheckInScoreRule.ReadinessScoreMaximum * 9m / 12m;
    private const int PreviousDayOffset = -1;

    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse(
        SampleUtcTimestamp, CultureInfo.InvariantCulture);
    private static readonly DateOnly TodayUtc = DateOnly.FromDateTime(NowUtc.UtcDateTime);

    [Fact]
    public void Prototype_Endpoints_And_Midpoint_Match_Specified_Arithmetic()
    {
        Assert.Equal(ExpectedMinimumScore, TodayCheckInScoreRule.CalculatePrototype(
            WorstFavorableScaleValue, WorstFavorableScaleValue, BestFavorableScaleValue));
        Assert.Equal(ExpectedMaximumScore, TodayCheckInScoreRule.CalculatePrototype(
            BestFavorableScaleValue, BestFavorableScaleValue, WorstFavorableScaleValue));
        Assert.Equal(ExpectedMidpointScore, TodayCheckInScoreRule.CalculatePrototype(
            MidpointScaleValue, MidpointScaleValue, MidpointScaleValue));
    }

    [Fact]
    public void One_Scale_Step_Changes_Raw_Score_By_Expected_Amount()
    {
        const decimal nextFavorableStep = 4m;
        var midpoint = TodayCheckInScoreRule.CalculatePrototype(
            MidpointScaleValue, MidpointScaleValue, MidpointScaleValue);

        Assert.Equal(ExpectedOneStepChange,
            TodayCheckInScoreRule.CalculatePrototype(nextFavorableStep, MidpointScaleValue,
                MidpointScaleValue) - midpoint);
        // Only the stress share moves, so higher stress lowers the score by that one share.
        Assert.Equal(-ExpectedOneStepChange,
            TodayCheckInScoreRule.CalculatePrototype(MidpointScaleValue, MidpointScaleValue,
                nextFavorableStep) - midpoint);
    }

    [Fact]
    public void Soreness_answers_do_not_move_the_score_or_its_band()
    {
        foreach (var sleep in new[] { 1m, 3m, 5m })
        foreach (var energy in new[] { 1m, 3m, 5m })
        foreach (var stress in new decimal?[] { null, 1m, 3m, 5m })
        {
            var lowest = Evaluate(sleep, energy, 1m, stress);
            var highest = Evaluate(sleep, energy, 5m, stress);

            Assert.Equal((lowest.RawScore, lowest.Band), (highest.RawScore, highest.Band));
        }
    }

    [Fact]
    public void Raw_Boundaries_Select_Bands_Before_Any_Display_Rounding()
    {
        var low = Evaluate(3m, 3m, MidpointScaleValue, 5m);
        var midpoint = Evaluate(MidpointScaleValue, MidpointScaleValue,
            MidpointScaleValue, MidpointScaleValue);
        var moderate = Evaluate(5m, 4m, MidpointScaleValue, 4m);
        var high = Evaluate(5m, 4m, MidpointScaleValue, 3m);

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
            Observation(sleep: null), NowUtc, UtcTimeZone);
        var fractional = TodayCheckInScoreRule.Evaluate(
            Observation(sleep: FractionalScaleValue), NowUtc, UtcTimeZone);

        Assert.Equal(CheckInScoreStatus.Incomplete, incomplete.Status);
        Assert.Null(incomplete.RawScore);
        Assert.Equal(CheckInScoreStatus.InvalidInput, fractional.Status);
        Assert.Null(fractional.RawScore);
    }

    [Fact]
    public void Yesterday_And_Changed_TimeZone_Do_Not_Reassign_Observation()
    {
        var yesterday = TodayCheckInScoreRule.Evaluate(
            Observation(date: TodayUtc.AddDays(PreviousDayOffset)), NowUtc, UtcTimeZone);
        var changedZone = TodayCheckInScoreRule.Evaluate(
            Observation(), NowUtc, TorontoTimeZone);

        Assert.Equal(CheckInScoreStatus.Stale, yesterday.Status);
        Assert.Null(yesterday.RawScore);
        Assert.Equal(CheckInScoreStatus.WrongTimeZoneAssociation, changedZone.Status);
        Assert.Null(changedZone.RawScore);
    }

    [Fact]
    public void Valid_CheckIn_Score_Is_Available_Without_Approval_Metadata()
    {
        var score = TodayCheckInScoreRule.Evaluate(Observation(), NowUtc, UtcTimeZone);

        Assert.Equal(CheckInScoreStatus.Available, score.Status);
        Assert.Equal(ExpectedMidpointScore, score.RawScore);
    }

    private static CheckInScoreResult Evaluate(decimal sleep, decimal energy, decimal? soreness, decimal? stress) =>
        TodayCheckInScoreRule.Evaluate(
            Observation(sleep: sleep, energy: energy, soreness: soreness, stress: stress),
            NowUtc, UtcTimeZone);

    private static TodayCheckInObservation Observation(
        DateOnly? date = null, decimal? sleep = MidpointScaleValue,
        decimal? energy = MidpointScaleValue, decimal? soreness = MidpointScaleValue,
        decimal? stress = MidpointScaleValue) =>
        new(date ?? TodayUtc, UtcTimeZone, NowUtc, 1, sleep, energy, soreness, stress);

}
