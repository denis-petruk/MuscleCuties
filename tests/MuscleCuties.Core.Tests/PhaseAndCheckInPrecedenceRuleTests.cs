using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class PhaseAndCheckInPrecedenceRuleTests
{
    private static CheckInScoreResult MakeCheckIn(CheckInScoreBand band) =>
        new(CheckInScoreStatus.Available, 80m, band, null);

    [Fact]
    public void Evaluate_NullContext_ReturnsSafetyBlocked()
    {
        var result = PhaseAndCheckInPrecedenceRule.Evaluate(null);

        Assert.Equal(PrecedenceProposalStatus.BlockedBySafety, result.Status);
        Assert.Equal(PrecedenceLevel.InjurySafety, result.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.Unavailable, result.EffectiveTier);
        Assert.False(result.IsApplied);
    }

    [Fact]
    public void Evaluate_ExplicitRestrictionConflict_TakesAbsolutePrecedenceOverHighReadiness()
    {
        var context = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: true,
            HasAcuteInjuryConflict: false,
            BaselineCycleTier: PhaseAdjustmentTier.Maintain,
            CheckInScore: MakeCheckIn(CheckInScoreBand.FeelingReady),
            ApplyChanges: true);

        var result = PhaseAndCheckInPrecedenceRule.Evaluate(context);

        Assert.Equal(PrecedenceProposalStatus.BlockedBySafety, result.Status);
        Assert.Equal(PrecedenceLevel.InjurySafety, result.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.Unavailable, result.EffectiveTier);
        Assert.False(result.IsApplied);
    }

    [Fact]
    public void Evaluate_AcuteInjuryConflict_TakesAbsolutePrecedence()
    {
        var context = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: true,
            BaselineCycleTier: PhaseAdjustmentTier.Maintain,
            CheckInScore: MakeCheckIn(CheckInScoreBand.FeelingReady),
            ApplyChanges: true);

        var result = PhaseAndCheckInPrecedenceRule.Evaluate(context);

        Assert.Equal(PrecedenceProposalStatus.BlockedBySafety, result.Status);
        Assert.Equal(PrecedenceLevel.InjurySafety, result.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.Unavailable, result.EffectiveTier);
    }

    [Fact]
    public void Evaluate_HighDailyReadiness_OverridesBaselineCycleDiscomfortToMaintain()
    {
        // Baseline indicates EasierDay due to cycle phase discomfort, but user is FeelingReady
        var context = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: false,
            BaselineCycleTier: PhaseAdjustmentTier.EasierDay,
            CheckInScore: MakeCheckIn(CheckInScoreBand.FeelingReady),
            ApplyChanges: false);

        var result = PhaseAndCheckInPrecedenceRule.Evaluate(context);

        Assert.Equal(PrecedenceProposalStatus.NoChangeNeeded, result.Status);
        Assert.Equal(PrecedenceLevel.DailyCheckInOverride, result.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.Maintain, result.EffectiveTier);
        Assert.Contains(PhaseAndCheckInPrecedenceRule.ReasonDailyReadinessOverridesCycleDiscomfort, result.PrecedenceReasons);
    }

    [Fact]
    public void Evaluate_DailyFatigue_DowngradesBaselineMaintainToEasierDay()
    {
        var context = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: false,
            BaselineCycleTier: PhaseAdjustmentTier.Maintain,
            CheckInScore: MakeCheckIn(CheckInScoreBand.ConsiderEasierDay),
            ApplyChanges: false);

        var result = PhaseAndCheckInPrecedenceRule.Evaluate(context);

        Assert.Equal(PrecedenceProposalStatus.PreviewPendingConfirmation, result.Status);
        Assert.Equal(PrecedenceLevel.DailyCheckInOverride, result.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.EasierDay, result.EffectiveTier);
        Assert.False(result.IsApplied);
        Assert.Contains(PhaseAndCheckInPrecedenceRule.ReasonDailyFatigueDowngradesToEasierDay, result.PrecedenceReasons);
    }

    [Fact]
    public void Evaluate_DailyFatigue_DowngradesBaselineMaintainToLighter()
    {
        var context = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: false,
            BaselineCycleTier: PhaseAdjustmentTier.Maintain,
            CheckInScore: MakeCheckIn(CheckInScoreBand.ConsiderLighterSession),
            ApplyChanges: false);

        var result = PhaseAndCheckInPrecedenceRule.Evaluate(context);

        Assert.Equal(PrecedenceProposalStatus.PreviewPendingConfirmation, result.Status);
        Assert.Equal(PrecedenceLevel.DailyCheckInOverride, result.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.Lighter, result.EffectiveTier);
        Assert.False(result.IsApplied);
    }

    [Fact]
    public void Evaluate_UnavailableCheckIn_DefaultsToBaselineCycleTier()
    {
        var context = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: false,
            BaselineCycleTier: PhaseAdjustmentTier.EasierDay,
            CheckInScore: null,
            ApplyChanges: false);

        var result = PhaseAndCheckInPrecedenceRule.Evaluate(context);

        Assert.Equal(PrecedenceProposalStatus.PreviewPendingConfirmation, result.Status);
        Assert.Equal(PrecedenceLevel.CyclePhaseBaseline, result.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.EasierDay, result.EffectiveTier);
        Assert.False(result.IsApplied);
    }

    [Fact]
    public void Evaluate_ExplicitApplyChangesGate_AppliesProposedDowngradeOnlyWhenTrue()
    {
        var unconfirmedContext = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: false,
            BaselineCycleTier: PhaseAdjustmentTier.Maintain,
            CheckInScore: MakeCheckIn(CheckInScoreBand.ConsiderEasierDay),
            ApplyChanges: false);

        var unconfirmedResult = PhaseAndCheckInPrecedenceRule.Evaluate(unconfirmedContext);
        Assert.Equal(PrecedenceProposalStatus.PreviewPendingConfirmation, unconfirmedResult.Status);
        Assert.False(unconfirmedResult.IsApplied);

        var confirmedContext = unconfirmedContext with { ApplyChanges = true };
        var confirmedResult = PhaseAndCheckInPrecedenceRule.Evaluate(confirmedContext);
        Assert.Equal(PrecedenceProposalStatus.Applied, confirmedResult.Status);
        Assert.True(confirmedResult.IsApplied);
    }
}
