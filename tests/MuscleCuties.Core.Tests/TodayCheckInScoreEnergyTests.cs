using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// S-01: low energy must lower the check-in score even when stress is unreported or was copied
/// from energy, and the readiness adjustment may only ever reduce the session.
/// </summary>
public sealed class TodayCheckInScoreEnergyTests
{
    private const string UtcTimeZone = "UTC";
    private const int Minimum = TodayCheckInScoreRule.ReadinessScaleMinimum;
    private const int Maximum = TodayCheckInScoreRule.ReadinessScaleMaximum;
    private const int BaselineMainSets = 3;
    private const int BaselineAccessorySets = 2;
    private static readonly DateTimeOffset NowUtc = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid MainId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AccessoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly int?[] StressAnswers = [null, 1, 2, 3, 4, 5];

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(Minimum)]
    public void Very_low_energy_with_great_sleep_and_no_soreness_is_not_feeling_ready(int? stress)
    {
        var score = Evaluate(sleep: Maximum, energy: Minimum, soreness: Minimum, stress);

        Assert.Equal(CheckInScoreStatus.Available, score.Status);
        Assert.NotEqual(CheckInScoreBand.FeelingReady, score.Band);
    }

    [Fact]
    public void Score_never_decreases_when_energy_rises()
    {
        foreach (var (sleep, soreness, stress) in Combinations())
        {
            decimal? previous = null;
            for (var energy = Minimum; energy <= Maximum; energy++)
            {
                var score = Evaluate(sleep, energy, soreness, stress).RawScore!.Value;
                if (previous is { } lower)
                    Assert.True(score >= lower, $"sleep {sleep}, soreness {soreness}, stress {stress}, energy {energy}");
                previous = score;
            }
        }
    }

    [Fact]
    public void Reported_stress_never_scores_higher_than_the_previous_arithmetic()
    {
        foreach (var (sleep, soreness, stress) in Combinations().Where(c => c.Stress is not null))
        {
            for (var energy = Minimum; energy <= Maximum; energy++)
            {
                var legacy = TodayCheckInScoreRule.ReadinessScoreMaximum *
                    ((sleep - Minimum) + (energy - Minimum) + (Maximum - soreness) + (Maximum - stress!.Value)) / 16m;
                Assert.True(Evaluate(sleep, energy, soreness, stress).RawScore <= legacy);
            }
        }
    }

    [Fact]
    public void Unreported_stress_is_scored_from_the_three_answered_ratings()
    {
        var score = Evaluate(sleep: Maximum, energy: Maximum, soreness: Minimum, stress: null);
        var fractional = TodayCheckInScoreRule.Evaluate(new TodayCheckInObservation(
            DateOnly.FromDateTime(NowUtc.UtcDateTime), UtcTimeZone, NowUtc, 1, 3m, 3m, 3m, 2.5m), NowUtc, UtcTimeZone);

        Assert.Equal(CheckInScoreStatus.Available, score.Status);
        Assert.Equal(TodayCheckInScoreRule.ReadinessScoreMaximum, score.RawScore);
        Assert.Equal(CheckInScoreStatus.InvalidInput, fractional.Status);
    }

    [Fact]
    public void Low_energy_reduces_optional_work_through_the_shipped_approval()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(
            Evaluate(sleep: Maximum, energy: Minimum, soreness: Minimum, stress: null), sleepHours: 8m));

        Assert.Equal(AccessoryId, decision.AdjustedPrescriptionId);
        Assert.Equal(BaselineAccessorySets - 1, decision.AdjustedSets);
        Assert.Equal(CycleReadinessPrescriptionRule.DailyReadinessAdjustment, decision.ReasonCode);
    }

    [Fact]
    public void Readiness_adjustment_never_increases_exposure()
    {
        foreach (var (sleep, soreness, stress) in Combinations())
        {
            for (var energy = Minimum; energy <= Maximum; energy++)
            {
                foreach (var sleepHours in new decimal?[] { null, 4m, 8m })
                {
                    var decision = CycleReadinessPrescriptionRule.Evaluate(
                        Input(Evaluate(sleep, energy, soreness, stress), sleepHours));
                    if (decision.AdjustedSets is not { } adjusted)
                        continue;

                    var baseline = decision.AdjustedPrescriptionId == MainId ? BaselineMainSets : BaselineAccessorySets;
                    Assert.True(adjusted < baseline);
                    Assert.Equal(AccessoryId, decision.AdjustedPrescriptionId);
                }
            }
        }
    }

    [Fact]
    public void Score_ruleset_version_is_its_own_and_bound_by_the_release_approval()
    {
        Assert.NotEqual(StrengthPlanPreviewRule.RulesetVersion, TodayCheckInScoreRule.RulesetVersion);
        Assert.Equal(TodayCheckInScoreRule.RulesetVersion,
            PlanningReleaseApproval.CycleReadinessPrescription!.RulesetVersion);
    }

    private static IEnumerable<(int Sleep, int Soreness, int? Stress)> Combinations() =>
        from sleep in Enumerable.Range(Minimum, Maximum)
        from soreness in Enumerable.Range(Minimum, Maximum)
        from stress in StressAnswers
        select (sleep, soreness, stress);

    private static CheckInScoreResult Evaluate(int sleep, int energy, int soreness, int? stress) =>
        TodayCheckInScoreRule.Evaluate(new TodayCheckInObservation(
            DateOnly.FromDateTime(NowUtc.UtcDateTime), UtcTimeZone, NowUtc, 1,
            sleep, energy, soreness, stress), NowUtc, UtcTimeZone);

    private static CycleReadinessPrescriptionInput Input(CheckInScoreResult score, decimal? sleepHours) =>
        new(CyclePhase.Unknown, PhaseReviewProvenance.Unknown, null, score, false,
        [
            new CycleReadinessExercise(MainId, BaselineMainSets, 0, IsOptional: false, IsAccessory: false),
            new CycleReadinessExercise(AccessoryId, BaselineAccessorySets, 1, IsOptional: true, IsAccessory: true)
        ],
            SleepHours: sleepHours,
            Approval: PlanningReleaseApproval.CycleReadinessPrescription);
}
