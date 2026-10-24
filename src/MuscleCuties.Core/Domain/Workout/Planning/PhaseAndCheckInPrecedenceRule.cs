namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum PrecedenceLevel
{
    InjurySafety,
    DailyCheckInOverride,
    CyclePhaseBaseline
}

public enum PrecedenceProposalStatus
{
    BlockedBySafety,
    NoChangeNeeded,
    PreviewPendingConfirmation,
    Applied
}

public sealed record PrecedenceEvaluationContext(
    bool HasExplicitRestrictionConflict,
    bool HasAcuteInjuryConflict,
    PhaseAdjustmentTier BaselineCycleTier,
    CheckInScoreResult? CheckInScore = null,
    bool ApplyChanges = false);

public sealed record PrecedenceEvaluationResult(
    PrecedenceProposalStatus Status,
    PrecedenceLevel ControllingPrecedence,
    PhaseAdjustmentTier EffectiveTier,
    bool IsApplied,
    IReadOnlyList<string> PrecedenceReasons);

/// <summary>
/// Enforces the clinical precedence hierarchy (Spec Section 13):
/// 1. Injury / Restriction Safety (highest authority, cannot be overridden).
/// 2. Daily Check-In Override (high daily readiness can override cycle phase baseline discomfort,
///    and daily fatigue can downgrade baseline plan).
/// 3. Cycle Phase Baseline (applies when daily check-in is unavailable or concordant).
///
/// Proposals are NEVER auto-applied without explicit user confirmation (ApplyChanges == true).
/// </summary>
public static class PhaseAndCheckInPrecedenceRule
{
    public const string ReasonSafetyBlocked = "Safety restriction or acute injury blocks session progression.";
    public const string ReasonDailyReadinessOverridesCycleDiscomfort = "High daily check-in readiness overrides baseline cycle phase discomfort to maintain plan.";
    public const string ReasonDailyFatigueDowngradesToEasierDay = "Daily check-in fatigue/symptoms downgrade baseline plan to easier day options.";
    public const string ReasonDailyFatigueDowngradesToLighter = "Daily check-in fatigue downgrades baseline plan to lighter session.";
    public const string ReasonCycleBaselineApplied = "Cycle phase baseline applied; daily check-in is unavailable or neutral.";
    public const string ReasonConcordantReadiness = "Daily readiness is concordant with baseline plan; no change required.";

    public static PrecedenceEvaluationResult Evaluate(PrecedenceEvaluationContext? context)
    {
        if (context is null)
        {
            return new PrecedenceEvaluationResult(
                Status: PrecedenceProposalStatus.BlockedBySafety,
                ControllingPrecedence: PrecedenceLevel.InjurySafety,
                EffectiveTier: PhaseAdjustmentTier.Unavailable,
                IsApplied: false,
                PrecedenceReasons: [ReasonSafetyBlocked]);
        }

        // Level 1: Injury / Restriction Safety takes absolute precedence
        if (context.HasExplicitRestrictionConflict || context.HasAcuteInjuryConflict)
        {
            return new PrecedenceEvaluationResult(
                Status: PrecedenceProposalStatus.BlockedBySafety,
                ControllingPrecedence: PrecedenceLevel.InjurySafety,
                EffectiveTier: PhaseAdjustmentTier.Unavailable,
                IsApplied: false,
                PrecedenceReasons: [ReasonSafetyBlocked]);
        }

        // Level 2 & 3: Check-In Override vs Cycle Phase Baseline
        var baselineTier = context.BaselineCycleTier;
        var checkIn = context.CheckInScore;

        PhaseAdjustmentTier effectiveTier;
        PrecedenceLevel controllingLevel;
        string reason;

        if (checkIn is { Status: CheckInScoreStatus.Available, Band: { } band })
        {
            if (band == CheckInScoreBand.FeelingReady)
            {
                if (baselineTier is PhaseAdjustmentTier.EasierDay or PhaseAdjustmentTier.Lighter)
                {
                    // High readiness overrides baseline cycle discomfort!
                    effectiveTier = PhaseAdjustmentTier.Maintain;
                    controllingLevel = PrecedenceLevel.DailyCheckInOverride;
                    reason = ReasonDailyReadinessOverridesCycleDiscomfort;
                }
                else
                {
                    effectiveTier = PhaseAdjustmentTier.Maintain;
                    controllingLevel = PrecedenceLevel.CyclePhaseBaseline;
                    reason = ReasonConcordantReadiness;
                }
            }
            else if (band == CheckInScoreBand.ConsiderEasierDay)
            {
                effectiveTier = PhaseAdjustmentTier.EasierDay;
                controllingLevel = baselineTier == PhaseAdjustmentTier.EasierDay
                    ? PrecedenceLevel.CyclePhaseBaseline
                    : PrecedenceLevel.DailyCheckInOverride;
                reason = baselineTier == PhaseAdjustmentTier.EasierDay
                    ? ReasonConcordantReadiness
                    : ReasonDailyFatigueDowngradesToEasierDay;
            }
            else if (band == CheckInScoreBand.ConsiderLighterSession)
            {
                effectiveTier = baselineTier == PhaseAdjustmentTier.EasierDay
                    ? PhaseAdjustmentTier.EasierDay
                    : PhaseAdjustmentTier.Lighter;
                controllingLevel = baselineTier == PhaseAdjustmentTier.Lighter
                    ? PrecedenceLevel.CyclePhaseBaseline
                    : PrecedenceLevel.DailyCheckInOverride;
                reason = baselineTier == PhaseAdjustmentTier.Lighter
                    ? ReasonConcordantReadiness
                    : ReasonDailyFatigueDowngradesToLighter;
            }
            else
            {
                effectiveTier = baselineTier;
                controllingLevel = PrecedenceLevel.CyclePhaseBaseline;
                reason = ReasonCycleBaselineApplied;
            }
        }
        else
        {
            // Daily check-in unavailable, incomplete, or invalid: baseline phase governs
            effectiveTier = baselineTier;
            controllingLevel = PrecedenceLevel.CyclePhaseBaseline;
            reason = ReasonCycleBaselineApplied;
        }

        // Confirmation Gate: Modifications require explicit ApplyChanges == true
        if (effectiveTier == PhaseAdjustmentTier.Maintain)
        {
            return new PrecedenceEvaluationResult(
                Status: PrecedenceProposalStatus.NoChangeNeeded,
                ControllingPrecedence: controllingLevel,
                EffectiveTier: effectiveTier,
                IsApplied: context.ApplyChanges,
                PrecedenceReasons: [reason]);
        }

        if (context.ApplyChanges)
        {
            return new PrecedenceEvaluationResult(
                Status: PrecedenceProposalStatus.Applied,
                ControllingPrecedence: controllingLevel,
                EffectiveTier: effectiveTier,
                IsApplied: true,
                PrecedenceReasons: [reason]);
        }

        return new PrecedenceEvaluationResult(
            Status: PrecedenceProposalStatus.PreviewPendingConfirmation,
            ControllingPrecedence: controllingLevel,
            EffectiveTier: effectiveTier,
            IsApplied: false,
            PrecedenceReasons: [reason]);
    }
}
