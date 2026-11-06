using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record BaselineExerciseDose(
    int ExerciseId,
    decimal BaseLoadKg,
    int BaseReps,
    int BaseSets,
    int BaseRir = 2,
    bool IsOptionalAccessory = false);

public sealed record DailyReadinessContext(
    int? ReadinessScore = null,
    CheckInScoreBand? ScoreBand = null,
    decimal? SleepHours = null,
    bool HasReportedFatigueOrSoreness = false,
    bool HasNewPainOrSafetyConcern = false);

public sealed record CyclePhaseContext(
    CyclePhase Phase = CyclePhase.Unknown,
    PhaseReviewProvenance Provenance = PhaseReviewProvenance.Unknown,
    bool IsCycleModeEnabled = false,
    bool PrefersLighterLoadHigherReps = false,
    bool RequestsAdditionalSets = false);

public sealed record ComposedSessionDose(
    int ExerciseId,
    decimal FinalLoadKg,
    int FinalReps,
    int FinalSets,
    int TargetRir,
    string ReasonCode,
    string DoseProvenance,
    bool ExtraSetsWithheldDueToFatigueSafeguard,
    bool IsComposed = true);

/// <summary>
/// WP04: Composed readiness and cycle load/rep/set adjustment enforcing single final dose authority.
/// Precedence: Safety/Injury restrictions -> Low readiness/fatigue reduction -> Cycle phase calibration.
/// Invariant: Additional cycle sets are strictly withheld when readiness is low (&lt; 75) or fatigue is present.
/// Reduced weight is not reduced work; tonnage must not balloon during fatigue.
/// </summary>
public static class ComposedReadinessCycleDoseRule
{
    public const int LowReadinessScoreThreshold = 75;
    public const decimal ShortSleepThresholdHours = 7.0m;
    public const decimal DefaultLoadIncrement = 2.5m;

    // Reason Codes
    public const string ReasonBaselinePreserved = "BaselinePreservedNormalReadiness";
    public const string ReasonLowReadinessLoadReduced = "LowReadinessCalibratedLoadReduction";
    public const string ReasonLowReadinessShortSleepLoadReduced = "LowReadinessShortSleepCalibratedReduction";
    public const string ReasonCyclePhaseLightenedWithExtraSets = "CyclePhaseHigherRepsLighterLoadWithExtraSet";
    public const string ReasonCyclePhaseLightenedSafeguardActive = "CyclePhaseLighterLoadExtraSetsWithheldFatigueSafeguard";
    public const string ReasonSafetyConcernWithheld = "SafetyConcernExerciseWithheld";
    public const string ReasonAlreadyComposedUnchanged = "AlreadyComposedDosePreserved";

    public static ComposedSessionDose ComposeDose(
        BaselineExerciseDose baseline,
        DailyReadinessContext readiness,
        CyclePhaseContext cycle,
        decimal loadIncrement = DefaultLoadIncrement,
        bool alreadyComposed = false)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(cycle);

        if (alreadyComposed)
        {
            return new ComposedSessionDose(
                baseline.ExerciseId,
                baseline.BaseLoadKg,
                baseline.BaseReps,
                baseline.BaseSets,
                baseline.BaseRir,
                ReasonAlreadyComposedUnchanged,
                "PreviouslyComposed",
                ExtraSetsWithheldDueToFatigueSafeguard: false,
                IsComposed: true);
        }

        // 1. Safety and pain stop takes ultimate precedence
        if (readiness.HasNewPainOrSafetyConcern)
        {
            return new ComposedSessionDose(
                baseline.ExerciseId,
                FinalLoadKg: 0m,
                FinalReps: 0,
                FinalSets: 0,
                TargetRir: 0,
                ReasonSafetyConcernWithheld,
                "SafetyStop",
                ExtraSetsWithheldDueToFatigueSafeguard: false,
                IsComposed: true);
        }

        var isShortSleep = readiness.SleepHours is >= 0m and < ShortSleepThresholdHours;
        var isLowReadinessBand = readiness.ScoreBand is CheckInScoreBand.ConsiderLighterSession or CheckInScoreBand.ConsiderEasierDay;
        var isLowReadinessScore = readiness.ReadinessScore is { } score && score < LowReadinessScoreThreshold;
        var isFatiguedOrLowReadiness = isLowReadinessBand || isLowReadinessScore || isShortSleep || readiness.HasReportedFatigueOrSoreness;

        var currentLoad = baseline.BaseLoadKg;
        var currentReps = baseline.BaseReps;
        var currentSets = baseline.BaseSets;
        var currentRir = baseline.BaseRir;

        var extraSetsWithheld = false;
        string reasonCode;
        string provenance;

        // 2. Evaluate Low Readiness / Sleep / Fatigue reduction
        if (isFatiguedOrLowReadiness)
        {
            // Calibrate load downward by ~10% (at least 1 equipment increment if load > increment)
            var reduction = Math.Max(loadIncrement, Math.Round(currentLoad * 0.10m / loadIncrement) * loadIncrement);
            currentLoad = Math.Max(0m, currentLoad - reduction);
            currentRir = Math.Min(4, currentRir + 1); // Add +1 RIR buffer for safety

            if (baseline.IsOptionalAccessory && currentSets > 1)
            {
                currentSets -= 1; // Reduce optional accessory volume
            }

            reasonCode = isShortSleep ? ReasonLowReadinessShortSleepLoadReduced : ReasonLowReadinessLoadReduced;
            provenance = "ReadinessAutoregulation";
        }
        else
        {
            reasonCode = ReasonBaselinePreserved;
            provenance = "Baseline";
        }

        // 3. Evaluate Cycle Phase Calibration (Luteal / Menstrual phase shifts towards lighter load / higher reps)
        var isSymptomaticPhase = cycle.Phase is CyclePhase.Luteal or CyclePhase.Menstrual;
        var isReliablePhase = cycle.Provenance is PhaseReviewProvenance.UserConfirmed or PhaseReviewProvenance.Estimated;

        if (cycle.IsCycleModeEnabled && isSymptomaticPhase && isReliablePhase)
        {
            if (cycle.PrefersLighterLoadHigherReps)
            {
                // Shift load down an extra increment if not already reduced by low readiness
                if (!isFatiguedOrLowReadiness && currentLoad > loadIncrement)
                {
                    currentLoad = Math.Max(0m, currentLoad - loadIncrement);
                }
                currentReps += 2; // Higher rep range (e.g. 8 -> 10 or 10 -> 12)
            }

            // Invariant: Extra cycle sets are allowed ONLY when not fatigued and readiness is adequate
            if (cycle.RequestsAdditionalSets)
            {
                if (isFatiguedOrLowReadiness)
                {
                    // FATIGUE SAFEGUARD TRIGGERED: Extra sets withheld!
                    extraSetsWithheld = true;
                    reasonCode = ReasonCyclePhaseLightenedSafeguardActive;
                    provenance = "CycleCalibrationWithFatigueSafeguard";
                }
                else
                {
                    currentSets += 1; // Add 1 conditional set
                    reasonCode = ReasonCyclePhaseLightenedWithExtraSets;
                    provenance = "CycleCalibrationDose";
                }
            }
        }

        return new ComposedSessionDose(
            baseline.ExerciseId,
            currentLoad,
            currentReps,
            currentSets,
            currentRir,
            reasonCode,
            provenance,
            extraSetsWithheld,
            IsComposed: true);
    }
}
