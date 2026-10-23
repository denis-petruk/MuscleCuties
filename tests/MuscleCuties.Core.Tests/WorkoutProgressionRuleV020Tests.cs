using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutProgressionRuleV020Tests
{
    private const int ExerciseId = 17;
    private const int LowerRepBoundary = 8;
    private const int CurrentRepTarget = 12;
    private const int CurrentCardioMinutes = 20;
    private const int LowerCardioMinutes = WorkoutTrainingPolicyV020.RegressionCardioMinimumMainMinutes;
    private static readonly DateTimeOffset Latest = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StrengthProgressionUsesSmallestIncrementAndResetsRepsToLowerBoundary()
    {
        var decision = WorkoutProgressionRuleV020.EvaluateStrength(WorkoutActivityType.GeneralStrength,
            ExerciseId, LowerRepBoundary, CurrentRepTarget,
            [StrengthEvidence(Latest, 0.5m, 1.25m), StrengthEvidence(Latest.AddDays(-7), 0.5m, 1.25m)], false);

        Assert.Equal(WorkoutAdjustmentKindV020.IncreaseLoad, decision.Kind);
        Assert.Equal(60.5m, decision.ProposedLoad);
        Assert.Equal(LowerRepBoundary, decision.ProposedRepetitions);
        Assert.True(decision.RequiresUserConfirmation);
    }

    [Fact]
    public void OneMissOffersRepeatAndPainUsesSeparatePathway()
    {
        var missed = StrengthEvidence(Latest, 0.5m, 1.25m) with
        {
            IsComplete = false,
            EverySetReachedTopOfRange = false,
            TargetEffortMet = false
        };
        var repeat = WorkoutProgressionRuleV020.EvaluateStrength(WorkoutActivityType.GeneralStrength,
            ExerciseId, LowerRepBoundary, CurrentRepTarget, [missed], false);
        var pain = WorkoutProgressionRuleV020.EvaluateStrength(WorkoutActivityType.GeneralStrength,
            ExerciseId, LowerRepBoundary, CurrentRepTarget, [StrengthEvidence(Latest, 0.5m, 1.25m, pain: true)], false);

        Assert.Equal(WorkoutAdjustmentKindV020.RepeatCurrentPrescription, repeat.Kind);
        Assert.Equal(WorkoutAdjustmentKindV020.SymptomReviewRequired, pain.Kind);
    }

    [Fact]
    public void TwoConsecutiveExposuresBelowRangeOfferOneLoadStepDown()
    {
        var belowLatest = StrengthEvidence(Latest, 2.5m, 5m) with
        {
            EverySetReachedTopOfRange = false,
            IsBelowLowerRepBoundary = true
        };
        var belowPrevious = belowLatest with
        {
            SessionId = Guid.NewGuid(),
            CompletedAtUtc = Latest.AddDays(-7)
        };

        var decision = WorkoutProgressionRuleV020.EvaluateStrength(WorkoutActivityType.GeneralStrength,
            ExerciseId, LowerRepBoundary, CurrentRepTarget, [belowLatest, belowPrevious], false);

        Assert.Equal(WorkoutAdjustmentKindV020.DecreaseLoad, decision.Kind);
        Assert.Equal(57.5m, decision.ProposedLoad);
        Assert.True(decision.RequiresUserConfirmation);
    }

    [Fact]
    public void CardioProgressionAndRegressionRequireTwoMatchingSessions()
    {
        var manageable = CardioEvidence(Latest) with { ReportedEasierOrAsPlanned = true };
        var previous = manageable with { SessionId = Guid.NewGuid(), CompletedAtUtc = Latest.AddDays(-5) };
        var progression = WorkoutProgressionRuleV020.EvaluateCardio(WorkoutActivityType.Cycling,
            [manageable, previous], CurrentCardioMinutes, false);
        var stopped = CardioEvidence(Latest) with { StoppedEarlyFromOrdinaryExertion = true };
        var stoppedPrevious = stopped with { SessionId = Guid.NewGuid(), CompletedAtUtc = Latest.AddDays(-5) };
        var regression = WorkoutProgressionRuleV020.EvaluateCardio(WorkoutActivityType.Cycling,
            [stopped, stoppedPrevious], CurrentCardioMinutes, false);

        Assert.Equal(WorkoutAdjustmentKindV020.IncreaseCardioDuration, progression.Kind);
        Assert.Equal(CurrentCardioMinutes + WorkoutTrainingPolicyV020.CardioProgressionMinutes,
            progression.ProposedMainMinutes);
        Assert.Equal(WorkoutAdjustmentKindV020.ReduceCardioDuration, regression.Kind);
        Assert.Equal(CurrentCardioMinutes - WorkoutTrainingPolicyV020.CardioRegressionMinutes,
            regression.ProposedMainMinutes);
    }

    [Fact]
    public void CardioRegressionNeverDropsBelowFiveMinutesAndLighterWeekIsOptional()
    {
        var stopped = CardioEvidence(Latest) with
        {
            PrescribedMainMinutes = LowerCardioMinutes,
            StoppedEarlyFromOrdinaryExertion = true
        };
        var previous = stopped with { SessionId = Guid.NewGuid(), CompletedAtUtc = Latest.AddDays(-4) };
        var regression = WorkoutProgressionRuleV020.EvaluateCardio(WorkoutActivityType.Running,
            [stopped, previous], LowerCardioMinutes, false);
        var notSelected = WorkoutProgressionRuleV020.ProposeLighterWeek(WorkoutActivityType.GeneralStrength,
            false, false);
        var selected = WorkoutProgressionRuleV020.ProposeLighterWeek(WorkoutActivityType.GeneralStrength,
            true, false);

        Assert.Equal(LowerCardioMinutes, regression.ProposedMainMinutes);
        Assert.Equal(WorkoutAdjustmentKindV020.None, notSelected.Kind);
        Assert.Equal(WorkoutAdjustmentKindV020.LighterWeek, selected.Kind);
        Assert.Equal(WorkoutTrainingPolicyV020.LighterWeekSetReduction, selected.SetCountReduction);
    }

    [Fact]
    public void CardioRegressionIgnoresEarlyStopsFromADifferentPrescription()
    {
        var current = CardioEvidence(Latest) with { StoppedEarlyFromOrdinaryExertion = true };
        var oldPrescription = CardioEvidence(Latest.AddDays(-4)) with
        {
            PrescribedMainMinutes = CurrentCardioMinutes - 5,
            StoppedEarlyFromOrdinaryExertion = true
        };

        var decision = WorkoutProgressionRuleV020.EvaluateCardio(WorkoutActivityType.Running,
            [current, oldPrescription], CurrentCardioMinutes, false);

        Assert.Equal(WorkoutAdjustmentKindV020.None, decision.Kind);
    }

    private static StrengthPerformanceEvidenceV020 StrengthEvidence(DateTimeOffset at,
        decimal incrementA, decimal incrementB, bool pain = false) =>
        new(Guid.NewGuid(), ExerciseId, at, true, true, true, false, pain,
            60m, [incrementA, incrementB]);

    private static CardioPerformanceEvidenceV020 CardioEvidence(DateTimeOffset at) =>
        new(Guid.NewGuid(), at, CurrentCardioMinutes, true, false, false, false);
}
