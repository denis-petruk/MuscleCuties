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
