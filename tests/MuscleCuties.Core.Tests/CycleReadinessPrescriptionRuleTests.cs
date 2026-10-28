using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CycleReadinessPrescriptionRuleTests
{
    private const int TwoSets = 2;
    private const int OneOptionalSet = 1;
    private const int HighEnergy = 5;
    private const int LowEnergy = 2;
    private const int LowDiscomfort = 1;
    private const int RelevantDiscomfort = 3;
    private static readonly Guid MainId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccessoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly Today = new(2026, 10, 28);

    [Theory]
    [InlineData(CyclePhase.Menstrual)]
    [InlineData(CyclePhase.Follicular)]
    [InlineData(CyclePhase.Ovulatory)]
    [InlineData(CyclePhase.Luteal)]
    public void Each_confirmed_phase_uses_only_its_personal_adverse_feeling(CyclePhase phase)
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(phase,
            PhaseReviewProvenance.UserConfirmed, new PhaseBaseline(RelevantDiscomfort, LowEnergy)));

        Assert.Equal(AccessoryId, decision.AdjustedPrescriptionId);
        Assert.Equal(OneOptionalSet, decision.AdjustedSets);
        Assert.Equal(CycleReadinessPrescriptionRule.ProvisionalPhaseAdjustment, decision.ReasonCode);
        Assert.Equal(phase, decision.EvidencedPhase);
    }

    [Theory]
    [InlineData(CyclePhase.Menstrual)]
    [InlineData(CyclePhase.Follicular)]
    [InlineData(CyclePhase.Ovulatory)]
    [InlineData(CyclePhase.Luteal)]
    public void Phase_alone_never_changes_dose(CyclePhase phase)
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(phase, PhaseReviewProvenance.UserConfirmed, null));
        Assert.Null(decision.AdjustedSets);
        Assert.Equal(CycleReadinessPrescriptionRule.NoAdjustment, decision.ReasonCode);
    }

    [Fact]
    public void Stale_check_in_does_not_reduce_dose()
    {
        var input = Input(CyclePhase.Unknown, PhaseReviewProvenance.Unknown, null) with
        {
            TodayCheckIn = new CheckInScoreResult(CheckInScoreStatus.Stale, null,
                CheckInScoreBand.ConsiderEasierDay, null)
        };
        Assert.Null(CycleReadinessPrescriptionRule.Evaluate(input).AdjustedSets);
    }

    [Fact]
    public void Short_sleep_with_current_check_in_selects_one_reduction()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Unknown,
            PhaseReviewProvenance.Unknown, null, CheckInScoreBand.FeelingReady) with
        {
            SleepHours = CycleReadinessPrescriptionRule.ShortSleepThresholdHours - OneOptionalSet
        });
        Assert.Equal(OneOptionalSet, decision.AdjustedSets);
        Assert.Equal(CycleReadinessConfidence.Current, decision.Confidence);
    }

    [Fact]
    public void Sleep_without_current_check_in_does_not_create_readiness()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Unknown,
            PhaseReviewProvenance.Unknown, null) with { SleepHours = CycleReadinessPrescriptionRule.ShortSleepThresholdHours - OneOptionalSet });
        Assert.Null(decision.AdjustedSets);
    }

    [Theory]
    [InlineData(PhaseReviewProvenance.Estimated)]
    [InlineData(PhaseReviewProvenance.Unknown)]
    public void Unconfirmed_phase_retains_normal_dose(PhaseReviewProvenance provenance)
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            provenance, new PhaseBaseline(RelevantDiscomfort, LowEnergy)));

        Assert.Null(decision.AdjustedPrescriptionId);
        Assert.Equal(CycleReadinessPrescriptionRule.NoAdjustment, decision.ReasonCode);
    }

    [Fact]
    public void Good_today_check_in_prevents_provisional_phase_reduction()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Menstrual,
            PhaseReviewProvenance.UserConfirmed, new PhaseBaseline(RelevantDiscomfort, LowEnergy),
            CheckInScoreBand.FeelingReady));

        Assert.Null(decision.AdjustedPrescriptionId);
    }

    [Fact]
    public void Poor_check_in_and_phase_feeling_reduce_only_one_optional_set()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            PhaseReviewProvenance.UserConfirmed, new PhaseBaseline(RelevantDiscomfort, LowEnergy),
            CheckInScoreBand.ConsiderEasierDay));

        Assert.Equal(AccessoryId, decision.AdjustedPrescriptionId);
        Assert.Equal(OneOptionalSet, decision.AdjustedSets);
        Assert.Equal(CycleReadinessPrescriptionRule.DailyReadinessAdjustment, decision.ReasonCode);
    }

    [Fact]
    public void Current_safety_concern_prevents_readiness_adjustment()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Unknown,
            PhaseReviewProvenance.Unknown, null, CheckInScoreBand.ConsiderEasierDay) with
            { HasNewPainOrSafetyConcern = true });

        Assert.Null(decision.AdjustedPrescriptionId);
        Assert.Equal(CycleReadinessPrescriptionRule.SafetyReviewRequired, decision.ReasonCode);
    }

    [Fact]
    public void Required_work_and_single_set_optional_work_are_preserved()
    {
        var input = Input(CyclePhase.Unknown, PhaseReviewProvenance.Unknown, null,
            CheckInScoreBand.ConsiderLighterSession) with
        {
            Exercises = [new CycleReadinessExercise(MainId, TwoSets, 0, false, false),
                new CycleReadinessExercise(AccessoryId, OneOptionalSet, 0, true, true)]
        };

        Assert.Null(CycleReadinessPrescriptionRule.Evaluate(input).AdjustedPrescriptionId);
    }

    [Fact]
    public void Repeated_trusted_symptoms_across_distinct_cycles_support_one_reduction()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            PhaseReviewProvenance.UserConfirmed, null) with
        {
            EvaluationDate = Today,
            PatternObservations = [Pattern("cycle-a", "check-a"), Pattern("cycle-b", "check-b"), Pattern("cycle-c", "check-c")]
        });

        Assert.Equal(OneOptionalSet, decision.AdjustedSets);
        Assert.Equal(CycleReadinessPrescriptionRule.RepeatedPhaseAdjustment, decision.ReasonCode);
        Assert.Equal(CycleReadinessConfidence.Repeated, decision.Confidence);
    }

    [Fact]
    public void Duplicate_untrusted_or_future_observations_do_not_establish_pattern()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            PhaseReviewProvenance.UserConfirmed, null) with
        {
            EvaluationDate = Today,
            PatternObservations = [Pattern("cycle-a", "same"), Pattern("cycle-b", "same"),
                Pattern("cycle-c", "third") with { TrustedCompletedWorkout = false },
                Pattern("cycle-d", "future") with { Date = Today }]
        });

        Assert.Null(decision.AdjustedSets);
        Assert.Equal(CycleReadinessConfidence.None, decision.Confidence);
    }

    [Fact]
    public void Estimated_phase_cannot_apply_repeated_pattern()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            PhaseReviewProvenance.Estimated, null) with
        {
            EvaluationDate = Today,
            PatternObservations = [Pattern("cycle-a", "check-a"), Pattern("cycle-b", "check-b"), Pattern("cycle-c", "check-c")]
        });
        Assert.Null(decision.AdjustedSets);
    }

    [Fact]
    public void Reliable_estimated_phase_can_use_repeated_personal_pattern()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            PhaseReviewProvenance.Estimated, null) with
        {
            PhaseEstimateReliable = true,
            EvaluationDate = Today,
            PatternObservations = [Pattern("cycle-a", "check-a"), Pattern("cycle-b", "check-b"), Pattern("cycle-c", "check-c")]
        });
        Assert.Equal(CycleReadinessPrescriptionRule.RepeatedPhaseAdjustment, decision.ReasonCode);
        Assert.Equal(CycleReadinessConfidence.Repeated, decision.Confidence);
    }

    [Theory]
    [InlineData(CyclePhase.Menstrual)]
    [InlineData(CyclePhase.Follicular)]
    [InlineData(CyclePhase.Ovulatory)]
    [InlineData(CyclePhase.Luteal)]
    public void Reliable_estimate_can_use_relevant_provisional_feeling(CyclePhase phase)
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(phase,
            PhaseReviewProvenance.Estimated, new PhaseBaseline(RelevantDiscomfort, LowEnergy)) with
        {
            PhaseEstimateReliable = true
        });
        Assert.Equal(CycleReadinessPrescriptionRule.ProvisionalPhaseAdjustment, decision.ReasonCode);
        Assert.Equal(phase, decision.EvidencedPhase);
    }

    [Fact]
    public void Old_pattern_observations_expire()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            PhaseReviewProvenance.UserConfirmed, null) with
        {
            EvaluationDate = Today,
            PatternObservations = [Pattern("cycle-a", "check-a"), Pattern("cycle-b", "check-b"),
                Pattern("cycle-c", "check-c") with { Date = Today.AddDays(-CycleReadinessPrescriptionRule.PatternObservationWindowDays - OneOptionalSet) }]
        });
        Assert.Null(decision.AdjustedSets);
    }

    [Fact]
    public void Current_good_check_in_overrides_repeated_low_readiness()
    {
        var decision = CycleReadinessPrescriptionRule.Evaluate(Input(CyclePhase.Luteal,
            PhaseReviewProvenance.UserConfirmed, null, CheckInScoreBand.FeelingReady) with
        {
            EvaluationDate = Today,
            PatternObservations = [Pattern("cycle-a", "check-a"), Pattern("cycle-b", "check-b"), Pattern("cycle-c", "check-c")]
        });
        Assert.Null(decision.AdjustedSets);
    }

    private static CycleReadinessPatternObservation Pattern(string cycle, string checkIn) =>
        new(CyclePhase.Luteal, cycle, checkIn, Today.AddDays(-OneOptionalSet), true, true);

    private static CycleReadinessPrescriptionInput Input(CyclePhase phase,
        PhaseReviewProvenance provenance, PhaseBaseline? baseline,
        CheckInScoreBand? band = null) => new(
            phase, provenance, baseline,
            band is null ? null : new CheckInScoreResult(CheckInScoreStatus.Available,
                null, band, new TodayCheckInObservation(default, string.Empty,
                    DateTimeOffset.MinValue, OneOptionalSet, null, null, null, null)),
            false,
            [new CycleReadinessExercise(MainId, TwoSets, 0, false, false),
                new CycleReadinessExercise(AccessoryId, TwoSets, 0, true, true)]);
}
