using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record CycleReadinessExercise(Guid PrescriptionId, int Sets, int OptionalSets,
    bool IsOptional, bool IsAccessory);

public sealed record CycleReadinessPrescriptionInput(
    CyclePhase Phase,
    PhaseReviewProvenance PhaseProvenance,
    PhaseBaseline? ProvisionalPhaseFeeling,
    CheckInScoreResult? TodayCheckIn,
    bool HasNewPainOrSafetyConcern,
    IReadOnlyList<CycleReadinessExercise> Exercises);

public sealed record CycleReadinessPrescriptionDecision(
    Guid? AdjustedPrescriptionId,
    int? AdjustedSets,
    string ReasonCode,
    CyclePhase? EvidencedPhase,
    long? CheckInRevision);

/// <summary>
/// Makes one bounded reduction to optional strength work. A phase is useful only
/// when explicitly confirmed; provisional onboarding feelings do not establish
/// a repeated pattern. Current check-in evidence is never counted twice.
/// </summary>
public static class CycleReadinessPrescriptionRule
{
    public const string PolicyVersion = "MC-CYCLE-READINESS-0.1";
    public const string NoAdjustment = "CycleReadinessNormalDose";
    public const string DailyReadinessAdjustment = "DailyReadinessOptionalSetReduced";
    public const string ProvisionalPhaseAdjustment = "ConfirmedPhaseProvisionalFeelingOptionalSetReduced";
    public const string SafetyReviewRequired = "CycleReadinessSafetyReviewRequired";
    private const int MinimumWorkingSets = 1;

    public static CycleReadinessPrescriptionDecision Evaluate(CycleReadinessPrescriptionInput? input)
    {
        if (input is null || input.Exercises is null || input.HasNewPainOrSafetyConcern ||
            input.Exercises.Any(exercise => exercise is null || exercise.PrescriptionId == Guid.Empty ||
                exercise.Sets < MinimumWorkingSets || exercise.OptionalSets < 0 ||
                exercise.OptionalSets >= exercise.Sets))
            return new(null, null, SafetyReviewRequired, null, null);

        var score = input.TodayCheckIn;
        var currentCheckIn = score is { Status: CheckInScoreStatus.Available,
            Band: { }, Observation: { } };
        var dailyReduction = currentCheckIn &&
            score!.Band is CheckInScoreBand.ConsiderLighterSession or CheckInScoreBand.ConsiderEasierDay;
        var confirmedPhase = (input.Phase is CyclePhase.Menstrual or CyclePhase.Follicular or
            CyclePhase.Ovulatory or CyclePhase.Luteal) &&
            input.PhaseProvenance == PhaseReviewProvenance.UserConfirmed;
        var phaseFeeling = confirmedPhase && input.ProvisionalPhaseFeeling is { } baseline &&
            PhaseAdjustmentRule.SelectTier(baseline, PhaseBaselineScale.QuizMappedOneToFive) is
                PhaseAdjustmentTier.Lighter or PhaseAdjustmentTier.EasierDay;
        var checkInFeelingReady = currentCheckIn && score!.Band == CheckInScoreBand.FeelingReady;
        var shouldReduce = dailyReduction || phaseFeeling && !checkInFeelingReady;
        var reason = dailyReduction ? DailyReadinessAdjustment : ProvisionalPhaseAdjustment;
        if (!shouldReduce)
            return new(null, null, NoAdjustment,
                confirmedPhase ? input.Phase : null,
                currentCheckIn ? score!.Observation!.Revision : null);

        var candidate = input.Exercises.FirstOrDefault(exercise =>
            exercise.IsOptional && exercise.IsAccessory &&
            exercise.Sets > MinimumWorkingSets);
        return candidate is null
            ? new(null, null, NoAdjustment,
                confirmedPhase ? input.Phase : null,
                currentCheckIn ? score!.Observation!.Revision : null)
            : new(candidate.PrescriptionId, candidate.Sets - PhaseAdjustmentRule.OptionalAccessorySetReduction,
                reason, confirmedPhase ? input.Phase : null,
                currentCheckIn ? score!.Observation!.Revision : null);
    }
}
