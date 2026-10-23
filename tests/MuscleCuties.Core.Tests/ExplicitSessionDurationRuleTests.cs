using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class ExplicitSessionDurationRuleTests
{
    private const int ExampleSets = 3;
    private const decimal ExampleActiveMinutesPerSet = 0.5m;
    private const decimal ExampleRestMinutes = 2m;
    private const decimal ExampleSetupMinutes = 1m;
    private const decimal ExampleWarmupMinutes = 5m;
    private const decimal ExampleCooldownMinutes = 3m;
    private const decimal ExpectedExampleMinutes = 14.5m;
    private const decimal ExpectedRoundMinutes = 13m;
    private const decimal ExpectedUnilateralMinutes = 7m;

    [Fact]
    public void StraightSets_And_Session_Match_Supplied_Example()
    {
        var block = ExplicitSessionDurationRule.StraightSets(new StraightSetDurationInput(
            ExampleSets, ExampleActiveMinutesPerSet,
            ExplicitSessionDurationRule.RestIntervalsBetweenSets(ExampleSets),
            ExampleRestMinutes, ExampleSetupMinutes, false, ActiveTimeCoverage.BothSidesIncluded));
        var session = ExplicitSessionDurationRule.Session(ExampleWarmupMinutes, [block], ExampleCooldownMinutes);

        Assert.True(block.IsAvailable);
        Assert.True(session.IsAvailable);
        Assert.Equal(ExpectedExampleMinutes, session.Minutes);
    }

    [Fact]
    public void RoundBlock_Counts_Rest_And_Transition_Once()
    {
        const int rounds = 3;
        const decimal activityMinutesPerRound = 2m;
        const decimal intraRoundRestMinutes = 0.5m;
        const decimal interRoundRestMinutes = 2m;
        const decimal transitionMinutes = 1.5m;

        var result = ExplicitSessionDurationRule.Rounds(new RoundBlockDurationInput(
            rounds, activityMinutesPerRound, intraRoundRestMinutes,
            interRoundRestMinutes, transitionMinutes));

        Assert.Equal(ExpectedRoundMinutes, result.Minutes);
    }

    [Fact]
    public void Unilateral_PerSide_Time_Includes_Both_Sides_Exactly_Once()
    {
        const int sets = 2;
        const decimal activeMinutesPerSide = 1m;
        const decimal restMinutes = 2m;
        const decimal setupMinutes = 1m;

        var result = ExplicitSessionDurationRule.StraightSets(new StraightSetDurationInput(
            sets, activeMinutesPerSide, ExplicitSessionDurationRule.RestIntervalsBetweenSets(sets),
            restMinutes, setupMinutes, true, ActiveTimeCoverage.PerSide));

        Assert.Equal(ExpectedUnilateralMinutes, result.Minutes);
    }

    [Fact]
    public void Missing_Or_Invalid_Timing_Is_Unavailable_Including_At_Session_Level()
    {
        var missing = ExplicitSessionDurationRule.StraightSets(new StraightSetDurationInput(
            ExampleSets, null, ExplicitSessionDurationRule.RestIntervalsBetweenSets(ExampleSets),
            ExampleRestMinutes, ExampleSetupMinutes, false, ActiveTimeCoverage.BothSidesIncluded));
        var invalid = ExplicitSessionDurationRule.Timed(-ExampleRestMinutes, ExampleSetupMinutes);
        var session = ExplicitSessionDurationRule.Session(ExampleWarmupMinutes, [missing], ExampleCooldownMinutes);

        Assert.Null(missing.Minutes);
        Assert.Contains(DurationIssue.MissingInput, missing.Issues);
        Assert.Null(invalid.Minutes);
        Assert.Contains(DurationIssue.InvalidInput, invalid.Issues);
        Assert.Null(session.Minutes);
        Assert.Contains(DurationIssue.MissingInput, session.Issues);
    }

    [Fact]
    public void Unilateral_Block_Requires_Explicit_Time_Coverage()
    {
        var result = ExplicitSessionDurationRule.StraightSets(new StraightSetDurationInput(
            ExampleSets, ExampleActiveMinutesPerSet,
            ExplicitSessionDurationRule.RestIntervalsBetweenSets(ExampleSets),
            ExampleRestMinutes, ExampleSetupMinutes, true, null));

        Assert.Null(result.Minutes);
        Assert.Contains(DurationIssue.MissingInput, result.Issues);
    }
}
