using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record CycleReadinessExercise(Guid PrescriptionId, int Sets, int OptionalSets,
    bool IsOptional, bool IsAccessory);

public sealed record CycleReadinessPatternObservation(
    CyclePhase Phase, string CycleId, string CheckInId, DateOnly Date,
    bool LowReadiness, bool TrustedCompletedWorkout);

public enum CycleReadinessConfidence { None, Provisional, Repeated, Current }

public sealed record CycleReadinessPrescriptionInput(
    CyclePhase Phase,
    PhaseReviewProvenance PhaseProvenance,
    PhaseBaseline? ProvisionalPhaseFeeling,
    CheckInScoreResult? TodayCheckIn,
    bool HasNewPainOrSafetyConcern,
    IReadOnlyList<CycleReadinessExercise> Exercises,
    IReadOnlyList<CycleReadinessPatternObservation>? PatternObservations = null,
    DateOnly? EvaluationDate = null,
    decimal? SleepHours = null,
    bool RoutineSymptoms = false,
    bool PhaseEstimateReliable = false,
    PlanningReviewApproval? Approval = null);

public sealed record CycleReadinessPrescriptionDecision(
    Guid? AdjustedPrescriptionId,
    int? AdjustedSets,
    string ReasonCode,
    CyclePhase? EvidencedPhase,
    long? CheckInRevision,
    CycleReadinessConfidence Confidence = CycleReadinessConfidence.None);

/// <summary>
/// Makes one bounded reduction to optional strength work. A phase is useful only
/// when explicitly confirmed or supported by recorded-cycle agreement; provisional onboarding feelings do not establish
/// a repeated pattern. Repeated low readiness requires trusted completed workouts across
/// distinct cycles and check-ins. Current evidence is never counted twice.
/// <para>
/// Precedence is part of the contract: invalid input and safety concerns withhold the session first,
/// then a missing or incomplete approval withholds the adjustment, and only then is evidence used.
/// Safety therefore never reports a policy gap, and a policy gap never masks a safety stop.
/// </para>
/// </summary>
public static class CycleReadinessPrescriptionRule
{
    public const string PolicyVersion = "MC-CYCLE-READINESS-0.3";
    public const string NoAdjustment = "CycleReadinessNormalDose";
    public const string DailyReadinessAdjustment = "DailyReadinessOptionalSetReduced";
    public const string ProvisionalPhaseAdjustment = "ConfirmedPhaseProvisionalFeelingOptionalSetReduced";
    public const string RepeatedPhaseAdjustment = "ConfirmedPhaseRepeatedLowReadinessOptionalSetReduced";
    public const string SafetyReviewRequired = "CycleReadinessSafetyReviewRequired";

    /// <summary>
    /// Returned when no complete approval record covers automated prescription changes. The dose is
    /// left untouched; this is the shipping state until a real review is recorded.
    /// </summary>
    public const string PolicyReviewMissing = "CycleReadinessPolicyReviewMissing";
    private const int MinimumWorkingSets = 1;
    public const int MinimumDistinctCyclesForPattern = 3;
    public const int PatternObservationWindowDays = 180;
    public const decimal ShortSleepThresholdHours = 7m;

    public static CycleReadinessPrescriptionDecision Evaluate(CycleReadinessPrescriptionInput? input)
    {
        if (input is null || input.Exercises is null || input.HasNewPainOrSafetyConcern ||
            input.Exercises.Any(exercise => exercise is null || exercise.PrescriptionId == Guid.Empty ||
                exercise.Sets < MinimumWorkingSets || exercise.OptionalSets < 0 ||
                exercise.OptionalSets >= exercise.Sets))
            return new(null, null, SafetyReviewRequired, null, null);

        // A dose change needs a real, complete approval record. Approval must cover both the
        // prescribed training and automated progression; other sections are not dose authority.
        if (!IsApproved(input.Approval))
            return new(null, null, PolicyReviewMissing, null, null);

        var score = input.TodayCheckIn;
        var currentCheckIn = score is { Status: CheckInScoreStatus.Available,
            Band: { }, Observation: { } };
        // This threshold is a conservative product choice, not a clinical diagnosis.
        var shortSleep = input.SleepHours is >= 0m and < ShortSleepThresholdHours;
        var dailyReduction = currentCheckIn && (shortSleep ||
            score!.Band is CheckInScoreBand.ConsiderLighterSession or CheckInScoreBand.ConsiderEasierDay);
        var reliablePhase = (input.Phase is CyclePhase.Menstrual or CyclePhase.Follicular or
            CyclePhase.Ovulatory or CyclePhase.Luteal) &&
            (input.PhaseProvenance == PhaseReviewProvenance.UserConfirmed ||
             input.PhaseProvenance == PhaseReviewProvenance.Estimated && input.PhaseEstimateReliable);
        // Owner decision S-03 / Decision 5 (2026-11-09): a cycle phase or estimate alone never changes the dose.
        // Only a current check-in signal (short sleep, lighter/easier band) or routine symptoms reduce one set.
        var shouldReduce = dailyReduction || input.RoutineSymptoms;
        const string reason = DailyReadinessAdjustment;
        const CycleReadinessConfidence confidence = CycleReadinessConfidence.Current;
        if (!shouldReduce)
            return new(null, null, NoAdjustment,
                reliablePhase ? input.Phase : null,
                currentCheckIn ? score!.Observation!.Revision : null);

        var candidate = input.Exercises.FirstOrDefault(exercise =>
            exercise.IsOptional && exercise.IsAccessory &&
            exercise.Sets > MinimumWorkingSets);
        return candidate is null
            ? new(null, null, NoAdjustment,
                reliablePhase ? input.Phase : null,
                currentCheckIn ? score!.Observation!.Revision : null)
            : new(candidate.PrescriptionId, candidate.Sets - PhaseAdjustmentRule.OptionalAccessorySetReduction,
                reason, reliablePhase ? input.Phase : null,
                currentCheckIn ? score!.Observation!.Revision : null, confidence);
    }

    private static bool IsApproved(PlanningReviewApproval? approval) =>
        approval is not null &&
        !string.IsNullOrWhiteSpace(approval.ApprovalReference) &&
        approval.ApprovedAtUtc != default &&
        string.Equals(approval.RulesetVersion, TodayCheckInScoreRule.RulesetVersion, StringComparison.Ordinal) &&
        string.Equals(approval.ReviewedTrainingPolicyVersion, PolicyVersion, StringComparison.Ordinal) &&
        approval.CoveredSections is not null &&
        approval.CoveredSections.Contains(PlanningReviewSection.TrainingPrescriptions) &&
        approval.CoveredSections.Contains(PlanningReviewSection.AutomatedProgression);
}
