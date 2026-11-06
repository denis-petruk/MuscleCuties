using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class ComposedReadinessCycleDoseRuleTests
{
    private static readonly BaselineExerciseDose DefaultBenchPress = new(
        ExerciseId: 101,
        BaseLoadKg: 60.0m,
        BaseReps: 8,
        BaseSets: 3,
        BaseRir: 2,
        IsOptionalAccessory: false);

    private static readonly BaselineExerciseDose DefaultCableFlye = new(
        ExerciseId: 102,
        BaseLoadKg: 15.0m,
        BaseReps: 12,
        BaseSets: 3,
        BaseRir: 2,
        IsOptionalAccessory: true);

    [Fact]
    public void NormalReadiness_PreservesBaselineDose()
    {
        var readiness = new DailyReadinessContext(ReadinessScore: 85, SleepHours: 8.0m);
        var cycle = new CyclePhaseContext(IsCycleModeEnabled: false);

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultBenchPress, readiness, cycle);

        Assert.Equal(60.0m, dose.FinalLoadKg);
        Assert.Equal(8, dose.FinalReps);
        Assert.Equal(3, dose.FinalSets);
        Assert.Equal(2, dose.TargetRir);
        Assert.Equal(ComposedReadinessCycleDoseRule.ReasonBaselinePreserved, dose.ReasonCode);
        Assert.False(dose.ExtraSetsWithheldDueToFatigueSafeguard);
        Assert.True(dose.IsComposed);
    }

    [Fact]
    public void LowReadinessScore_ReducesLoadAndAddsRirBuffer()
    {
        var readiness = new DailyReadinessContext(ReadinessScore: 60, SleepHours: 8.0m);
        var cycle = new CyclePhaseContext(IsCycleModeEnabled: false);

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultBenchPress, readiness, cycle);

        // 60kg reduced by 10% (6kg -> rounded to 5kg increment or nearest 2.5kg = 55kg)
        Assert.True(dose.FinalLoadKg < DefaultBenchPress.BaseLoadKg);
        Assert.Equal(55.0m, dose.FinalLoadKg);
        Assert.Equal(3, dose.TargetRir); // +1 RIR buffer
        Assert.Equal(ComposedReadinessCycleDoseRule.ReasonLowReadinessLoadReduced, dose.ReasonCode);
    }

    [Fact]
    public void ShortSleep_TriggersCalibratedLoadReduction()
    {
        var readiness = new DailyReadinessContext(ReadinessScore: 80, SleepHours: 5.5m);
        var cycle = new CyclePhaseContext(IsCycleModeEnabled: false);

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultBenchPress, readiness, cycle);

        Assert.True(dose.FinalLoadKg < DefaultBenchPress.BaseLoadKg);
        Assert.Equal(ComposedReadinessCycleDoseRule.ReasonLowReadinessShortSleepLoadReduced, dose.ReasonCode);
    }

    [Fact]
    public void LowReadiness_ReducesOptionalAccessorySets()
    {
        var readiness = new DailyReadinessContext(ReadinessScore: 50);
        var cycle = new CyclePhaseContext();

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultCableFlye, readiness, cycle);

        Assert.Equal(2, dose.FinalSets); // 3 -> 2 sets for optional accessory
    }

    [Fact]
    public void CycleMode_WellRested_AddsExtraSetAndShiftsToHigherReps()
    {
        var readiness = new DailyReadinessContext(ReadinessScore: 90, SleepHours: 8.5m);
        var cycle = new CyclePhaseContext(
            Phase: CyclePhase.Luteal,
            Provenance: PhaseReviewProvenance.UserConfirmed,
            IsCycleModeEnabled: true,
            PrefersLighterLoadHigherReps: true,
            RequestsAdditionalSets: true);

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultBenchPress, readiness, cycle);

        Assert.Equal(57.5m, dose.FinalLoadKg); // shifted down 1 increment
        Assert.Equal(10, dose.FinalReps);      // 8 -> 10 reps (+2)
        Assert.Equal(4, dose.FinalSets);       // 3 -> 4 sets (+1 conditional set)
        Assert.False(dose.ExtraSetsWithheldDueToFatigueSafeguard);
        Assert.Equal(ComposedReadinessCycleDoseRule.ReasonCyclePhaseLightenedWithExtraSets, dose.ReasonCode);
    }

    [Fact]
    public void CycleMode_WithFatigueOrLowReadiness_TriggersFatigueSafeguard_WithholdingExtraSets()
    {
        // Crucial invariant: Denis requests cycle mode, but low readiness / fatigue must NOT allow extra sets!
        var readiness = new DailyReadinessContext(
            ReadinessScore: 65, // < 75 threshold
            SleepHours: 6.0m,
            HasReportedFatigueOrSoreness: true);

        var cycle = new CyclePhaseContext(
            Phase: CyclePhase.Menstrual,
            Provenance: PhaseReviewProvenance.UserConfirmed,
            IsCycleModeEnabled: true,
            PrefersLighterLoadHigherReps: true,
            RequestsAdditionalSets: true);

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultBenchPress, readiness, cycle);

        Assert.True(dose.FinalLoadKg < DefaultBenchPress.BaseLoadKg);
        Assert.Equal(10, dose.FinalReps); // Rep shift preserved
        Assert.Equal(3, dose.FinalSets);  // Extra set was WITHHELD (remains 3, NOT 4!)
        Assert.True(dose.ExtraSetsWithheldDueToFatigueSafeguard);
        Assert.Equal(ComposedReadinessCycleDoseRule.ReasonCyclePhaseLightenedSafeguardActive, dose.ReasonCode);
    }

    [Fact]
    public void SafetyConcernOrPain_WithholdsSessionImmediately()
    {
        var readiness = new DailyReadinessContext(HasNewPainOrSafetyConcern: true);
        var cycle = new CyclePhaseContext();

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultBenchPress, readiness, cycle);

        Assert.Equal(0m, dose.FinalLoadKg);
        Assert.Equal(0, dose.FinalSets);
        Assert.Equal(0, dose.FinalReps);
        Assert.Equal(ComposedReadinessCycleDoseRule.ReasonSafetyConcernWithheld, dose.ReasonCode);
    }

    [Fact]
    public void AlreadyComposed_DoesNotReapplyAdjustmentsTwice()
    {
        var readiness = new DailyReadinessContext(ReadinessScore: 50);
        var cycle = new CyclePhaseContext();

        var dose = ComposedReadinessCycleDoseRule.ComposeDose(DefaultBenchPress, readiness, cycle, alreadyComposed: true);

        Assert.Equal(DefaultBenchPress.BaseLoadKg, dose.FinalLoadKg);
        Assert.Equal(DefaultBenchPress.BaseSets, dose.FinalSets);
        Assert.Equal(ComposedReadinessCycleDoseRule.ReasonAlreadyComposedUnchanged, dose.ReasonCode);
    }
}
