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
    bool RoutineSymptoms = false);

public sealed record CycleReadinessPrescriptionDecision(
    Guid? AdjustedPrescriptionId,
    int? AdjustedSets,
    string ReasonCode,
    CyclePhase? EvidencedPhase,
    long? CheckInRevision,
    CycleReadinessConfidence Confidence = CycleReadinessConfidence.None);

/// <summary>
/// Makes one bounded reduction to optional strength work. A phase is useful only
/// when explicitly confirmed; provisional onboarding feelings do not establish
/// a repeated pattern. Repeated low readiness requires trusted completed workouts across
/// distinct cycles and check-ins. Current evidence is never counted twice.
/// </summary>
public static class CycleReadinessPrescriptionRule
{
    public const string PolicyVersion = "MC-CYCLE-READINESS-0.2";
    public const string NoAdjustment = "CycleReadinessNormalDose";
    public const string DailyReadinessAdjustment = "DailyReadinessOptionalSetReduced";
    public const string ProvisionalPhaseAdjustment = "ConfirmedPhaseProvisionalFeelingOptionalSetReduced";
    public const string RepeatedPhaseAdjustment = "ConfirmedPhaseRepeatedLowReadinessOptionalSetReduced";
    public const string SafetyReviewRequired = "CycleReadinessSafetyReviewRequired";
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

        var score = input.TodayCheckIn;
        var currentCheckIn = score is { Status: CheckInScoreStatus.Available,
            Band: { }, Observation: { } };
        // This threshold is a conservative product choice, not a clinical diagnosis.
        var shortSleep = input.SleepHours is >= 0m and < ShortSleepThresholdHours;
        var dailyReduction = currentCheckIn && (shortSleep ||
            score!.Band is CheckInScoreBand.ConsiderLighterSession or CheckInScoreBand.ConsiderEasierDay);
        var confirmedPhase = (input.Phase is CyclePhase.Menstrual or CyclePhase.Follicular or
            CyclePhase.Ovulatory or CyclePhase.Luteal) &&
            input.PhaseProvenance == PhaseReviewProvenance.UserConfirmed;
        var phaseFeeling = confirmedPhase && input.ProvisionalPhaseFeeling is { } baseline &&
            PhaseAdjustmentRule.SelectTier(baseline, PhaseBaselineScale.QuizMappedOneToFive) is
                PhaseAdjustmentTier.Lighter or PhaseAdjustmentTier.EasierDay;
        var repeatedFeeling = confirmedPhase && HasRepeatedPattern(input);
        var checkInFeelingReady = currentCheckIn && score!.Band == CheckInScoreBand.FeelingReady;
        var shouldReduce = dailyReduction || input.RoutineSymptoms || (phaseFeeling || repeatedFeeling) && !checkInFeelingReady;
        var reason = dailyReduction || input.RoutineSymptoms ? DailyReadinessAdjustment : repeatedFeeling ? RepeatedPhaseAdjustment : ProvisionalPhaseAdjustment;
        var confidence = dailyReduction || input.RoutineSymptoms ? CycleReadinessConfidence.Current : repeatedFeeling
            ? CycleReadinessConfidence.Repeated : CycleReadinessConfidence.Provisional;
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
                currentCheckIn ? score!.Observation!.Revision : null, confidence);
    }

    private static bool HasRepeatedPattern(CycleReadinessPrescriptionInput input)
    {
        if (input.EvaluationDate is not { } evaluationDate || input.PatternObservations is null)
            return false;
        var eligible = input.PatternObservations.Where(observation => observation is not null &&
            observation.Phase == input.Phase && observation.Date < evaluationDate &&
            evaluationDate.DayNumber - observation.Date.DayNumber <= PatternObservationWindowDays &&
            observation.TrustedCompletedWorkout && observation.LowReadiness &&
            !string.IsNullOrWhiteSpace(observation.CycleId) && !string.IsNullOrWhiteSpace(observation.CheckInId)).ToArray();
        // Each cycle must have its own check-in; repeated rows are not additional evidence.
        var candidates = eligible.GroupBy(observation => observation.CycleId, StringComparer.Ordinal)
            .Select(group => group.Select(observation => observation.CheckInId).Distinct(StringComparer.Ordinal).ToArray())
            .ToArray();
        var assignedCycles = new Dictionary<string, int>(StringComparer.Ordinal);
        var matched = 0;
        for (var index = 0; index < candidates.Length; index++)
        {
            if (AssignCheckIn(candidates, index, assignedCycles, new HashSet<string>(StringComparer.Ordinal)) &&
                ++matched >= MinimumDistinctCyclesForPattern)
                return true;
        }
        return false;
    }

    private static bool AssignCheckIn(string[][] candidates, int index, Dictionary<string, int> assignedCycles,
        HashSet<string> visited)
    {
        foreach (var checkInId in candidates[index])
        {
            if (!visited.Add(checkInId)) continue;
            if (assignedCycles.TryGetValue(checkInId, out var previousCycle) &&
                !AssignCheckIn(candidates, previousCycle, assignedCycles, visited)) continue;
            assignedCycles[checkInId] = index;
            return true;
        }
        return false;
    }
}
