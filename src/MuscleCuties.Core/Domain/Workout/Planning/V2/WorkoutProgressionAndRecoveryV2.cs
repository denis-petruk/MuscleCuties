using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.V2;

public enum ProgressionDecisionKindV2
{
    CalibrationRequired = 0,
    Maintain = 1,
    RecommendIncrease = 2,
    RecommendDecrease = 3,
    RecommendGlutePlateauPlusOneSet = 4
}

public sealed record StrengthProgressionDecisionV2(
    int ExerciseId,
    ProgressionDecisionKindV2 Decision,
    decimal? RecommendedLoadKg,
    string ReasonCode,
    int? RecommendedTargetRepsAfterIncrease = null,
    int? RecommendedAdditionalWeeklySets = null);

public sealed record PainReturnEligibilityV2(
    bool ReassessmentDue,
    bool CanReturnWithConservativeLoad,
    decimal? ConservativeLoadKg,
    int RemainingConservativeSessions,
    string ReasonCode);

public sealed record CardioProgressionRecommendationV2(
    WorkoutActivityType Activity,
    int? RecommendedDurationMinutes,
    decimal? RecommendedDistanceKm,
    decimal? DerivedPaceMinPerKm,
    string ReasonCode);

/// <summary>
/// Packages F, G (pain return), and H (cardio progression):
/// - Gate 71: RIR and technique are optional; unknown remains unknown.
/// - Gate 72: 2 comparable qualifying sessions at upper rep bound -> recommend next increment.
/// - Gate 73: 2 consecutive comparable sessions below lower rep bound -> recommend smaller increment.
/// - Gate 74: 3-4 week glute plateau with consistency & recovery -> +1 direct set before swap.
/// - Gate 75: Unilateral progression gated on weaker side.
/// - Gate 77: Unknown initial load -> CalibrationRequired (bodyweight allows 0 kg).
/// - Gate 88: 72-hour reassessment; time alone never clears; 10-15% conservative return for 2 sessions.
/// </summary>
public static class WorkoutProgressionAndRecoveryV2
{
    public const string CalibrationRequiredCode = "InitialLoadCalibrationRequired";
    public const string BodyweightZeroLoadCode = "BodyweightZeroAddedLoad";
    public const string DoubleProgressionEarnedCode = "DoubleProgressionLoadIncreaseRecommended";
    public const string TwoSessionDeficitDecreaseCode = "ConsecutiveBelowMinRepDecreaseRecommended";
    public const string SingleDeficitHoldsLoadCode = "SingleUnderTargetSessionMaintainsLoad";
    public const string TechniqueBlockedIncreaseCode = "UnacceptableTechniqueBlocksIncrease";
    public const string GlutePlateauAddOneSetCode = "GlutePlateauAddOneDirectSetRecommended";
    public const string MaintainCurrentLoadCode = "MaintainCurrentLoad";
    public const string DeloadSessionsExcludedCode = "DeloadSessionsExcludedFromRegression";

    public static StrengthProgressionDecisionV2 EvaluateStrengthProgression(
        ExerciseCatalogItemV2 exercise,
        int prescribedSets,
        int minReps,
        int maxReps,
        int targetRir,
        IReadOnlyList<ObservedSetRecordV2>? history,
        TrainingFocus focus = TrainingFocus.Balanced,
        bool recoveryIsAdequate = true)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var workingHistory = (history ?? [])
            .Where(s => s.ExerciseId == exercise.ExerciseId &&
                        s.SetKind == SetKindV2.Working &&
                        !s.IsDeloadSession &&
                        s.RepsCompleted > 0)
            .ToArray();

        if (workingHistory.Length == 0)
        {
            if (exercise.IsBodyweightOnly)
                return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, 0m, BodyweightZeroLoadCode);

            return new(exercise.ExerciseId, ProgressionDecisionKindV2.CalibrationRequired, null, CalibrationRequiredCode);
        }

        // Group by completed session in chronological order (most recent last).
        var sessions = workingHistory
            .GroupBy(s => s.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                CompletedAt = g.Max(s => s.CompletedAtUtc),
                Sets = g.OrderBy(s => s.SetNumber).ToArray()
            })
            .Where(s => s.Sets.Length >= prescribedSets)
            .OrderBy(s => s.CompletedAt)
            .ToArray();

        if (sessions.Length == 0)
        {
            var latestLoad = workingHistory.OrderByDescending(s => s.CompletedAtUtc).First().LoadKg;
            if (!exercise.IsBodyweightOnly && exercise.MinimumLoadIncrementKg is null && latestLoad <= 0m)
                return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, null, MaintainCurrentLoadCode);
            if (!exercise.IsBodyweightOnly && latestLoad <= 0m)
                return new(exercise.ExerciseId, ProgressionDecisionKindV2.CalibrationRequired, null, CalibrationRequiredCode);

            return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, latestLoad, MaintainCurrentLoadCode);
        }

        var latestSession = sessions[^1];
        var currentLoad = latestSession.Sets[0].LoadKg;
        if (!exercise.IsBodyweightOnly && exercise.MinimumLoadIncrementKg is null && currentLoad <= 0m)
            return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, null, MaintainCurrentLoadCode);
        if (!exercise.IsBodyweightOnly && currentLoad <= 0m)
            return new(exercise.ExerciseId, ProgressionDecisionKindV2.CalibrationRequired, null, CalibrationRequiredCode);

        var increment = exercise.MinimumLoadIncrementKg ?? 0m;

        // Check last two comparable sessions at the same load.
        if (sessions.Length >= 2)
        {
            var prevSession = sessions[^2];
            var sameLoad = prevSession.Sets.All(s => s.LoadKg == currentLoad) &&
                           latestSession.Sets.All(s => s.LoadKg == currentLoad);

            if (sameLoad)
            {
                // Gate 72: Double progression (RIR optional; if RIR is present, must not be harder than 0 when target >= 2).
                var bothQualifyForIncrease =
                    QualifiesForIncrease(prevSession.Sets, exercise.IsUnilateral, maxReps, targetRir) &&
                    QualifiesForIncrease(latestSession.Sets, exercise.IsUnilateral, maxReps, targetRir);

                if (bothQualifyForIncrease)
                {
                    if (increment > 0m)
                    {
                        return new(
                            exercise.ExerciseId,
                            ProgressionDecisionKindV2.RecommendIncrease,
                            currentLoad + increment,
                            DoubleProgressionEarnedCode,
                            RecommendedTargetRepsAfterIncrease: minReps);
                    }

                    return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, currentLoad, MaintainCurrentLoadCode);
                }

                // Gate 73: Two consecutive comparable sessions strictly below minReps -> recommend reduction (works with or without RIR).
                var bothUnderMin =
                    IsBelowMinimumReps(prevSession.Sets, exercise.IsUnilateral, minReps) &&
                    IsBelowMinimumReps(latestSession.Sets, exercise.IsUnilateral, minReps);

                if (bothUnderMin && increment > 0m)
                {
                    var reduced = Math.Max(0m, currentLoad - increment);
                    return new(
                        exercise.ExerciseId,
                        ProgressionDecisionKindV2.RecommendDecrease,
                        reduced,
                        TwoSessionDeficitDecreaseCode);
                }
            }
        }

        if (IsBelowMinimumReps(latestSession.Sets, exercise.IsUnilateral, minReps))
        {
            return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, currentLoad, SingleDeficitHoldsLoadCode);
        }

        if (latestSession.Sets.Any(s => !s.TechniqueAcceptable))
        {
            return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, currentLoad, TechniqueBlockedIncreaseCode);
        }

        // Gate 74: 3-4 week glute plateau detection (+1 direct glute set when consistent and recovered).
        if (focus == TrainingFocus.GluteSpecialization &&
            exercise.IsGluteDominant &&
            recoveryIsAdequate &&
            sessions.Length >= 4)
        {
            var windowDays = (latestSession.CompletedAt - sessions[0].CompletedAt).TotalDays;
            var allSameLoad = sessions.TakeLast(4).All(s => s.Sets.All(x => x.LoadKg == currentLoad));
            var noRecentIncrease = !QualifiesForIncrease(latestSession.Sets, exercise.IsUnilateral, maxReps, targetRir);

            if (windowDays >= 21 && allSameLoad && noRecentIncrease)
            {
                return new(
                    exercise.ExerciseId,
                    ProgressionDecisionKindV2.RecommendGlutePlateauPlusOneSet,
                    currentLoad,
                    GlutePlateauAddOneSetCode,
                    RecommendedAdditionalWeeklySets: 1);
            }
        }

        return new(exercise.ExerciseId, ProgressionDecisionKindV2.Maintain, currentLoad, MaintainCurrentLoadCode);
    }

    public static PainReturnEligibilityV2 EvaluatePainReturn(
        PainRestrictionV2 restriction,
        DateTimeOffset nowUtc,
        decimal? priorWorkingLoadKg,
        decimal? incrementKg)
    {
        ArgumentNullException.ThrowIfNull(restriction);

        var reassessmentDue = restriction.ReportedAtUtc is { } reported &&
            (nowUtc - reported).TotalHours >= WorkoutPolicyV2.PainReassessmentHours;

        // Passage of time alone NEVER clears a restriction.
        if (!restriction.IsResolved)
        {
            return new PainReturnEligibilityV2(
                ReassessmentDue: reassessmentDue,
                CanReturnWithConservativeLoad: false,
                ConservativeLoadKg: null,
                RemainingConservativeSessions: WorkoutPolicyV2.ConservativeReturnSessionCount,
                ReasonCode: reassessmentDue ? "PainReassessmentDue" : "PainRestrictionActive");
        }

        var remaining = Math.Max(0, WorkoutPolicyV2.ConservativeReturnSessionCount - restriction.ConservativeReturnSessionsCompleted);
        if (remaining == 0 || priorWorkingLoadKg is null or <= 0m)
        {
            return new PainReturnEligibilityV2(
                ReassessmentDue: false,
                CanReturnWithConservativeLoad: false,
                ConservativeLoadKg: priorWorkingLoadKg,
                RemainingConservativeSessions: 0,
                ReasonCode: "NormalReturnPermitted");
        }

        var rawConservative = priorWorkingLoadKg.Value * WorkoutPolicyV2.ConservativeReturnLoadMultiplier;
        var conservativeLoad = incrementKg is > 0m
            ? Math.Floor(rawConservative / incrementKg.Value) * incrementKg.Value
            : Math.Floor(rawConservative * 10m) / 10m;
        if (conservativeLoad <= 0m)
            conservativeLoad = rawConservative;

        return new PainReturnEligibilityV2(
            ReassessmentDue: false,
            CanReturnWithConservativeLoad: true,
            ConservativeLoadKg: Math.Max(0m, conservativeLoad),
            RemainingConservativeSessions: remaining,
            ReasonCode: "ConservativeReturnLoadApplied");
    }

    public static CardioProgressionRecommendationV2 EvaluateCardioProgression(
        WorkoutActivityType activity,
        IReadOnlyList<CardioActualRecordV2>? history)
    {
        var matching = (history ?? [])
            .Where(h => h.Activity == activity && (h.DurationMinutes is > 0 || h.DistanceKm is > 0m))
            .OrderBy(h => h.LocalDate)
            .ToArray();

        if (matching.Length == 0)
        {
            return new CardioProgressionRecommendationV2(
                activity,
                RecommendedDurationMinutes: 20,
                RecommendedDistanceKm: null,
                DerivedPaceMinPerKm: null,
                ReasonCode: "InitialCardioBaseline");
        }

        var latest = matching[^1];
        decimal? derivedPace = latest.DurationMinutes is > 0 && latest.DistanceKm is > 0m
            ? Math.Round(latest.DurationMinutes.Value / latest.DistanceKm.Value, 2, MidpointRounding.AwayFromZero)
            : null;

        // Gate 82: Adjust one primary variable gradually (e.g., +5 min duration), never a compulsory 10% rule.
        if (matching.Length >= 2 && latest.DurationMinutes is > 0)
        {
            var prev = matching[^2];
            if (prev.DurationMinutes == latest.DurationMinutes && (latest.Rpe is null or <= 5))
            {
                return new CardioProgressionRecommendationV2(
                    activity,
                    RecommendedDurationMinutes: Math.Min(60, latest.DurationMinutes.Value + 5),
                    RecommendedDistanceKm: latest.DistanceKm,
                    DerivedPaceMinPerKm: derivedPace,
                    ReasonCode: "SingleVariableDurationStepRecommended");
            }
        }

        return new CardioProgressionRecommendationV2(
            activity,
            RecommendedDurationMinutes: latest.DurationMinutes,
            RecommendedDistanceKm: latest.DistanceKm,
            DerivedPaceMinPerKm: derivedPace,
            ReasonCode: "MaintainCardioBaseline");
    }

    private static int EffectiveReps(ObservedSetRecordV2 set, bool isUnilateral)
    {
        if (!isUnilateral)
            return set.RepsCompleted;

        if (set.LeftSideReps.HasValue && set.RightSideReps.HasValue)
            return Math.Min(set.LeftSideReps.Value, set.RightSideReps.Value);

        return set.RepsCompleted;
    }

    private static bool QualifiesForIncrease(
        ObservedSetRecordV2[] sets,
        bool isUnilateral,
        int maxReps,
        int targetRir) =>
        sets.All(s =>
            s.TechniqueAcceptable &&
            EffectiveReps(s, isUnilateral) >= maxReps &&
            (s.RirActual is null || s.RirActual.Value >= Math.Max(0, targetRir - 1)));

    private static bool IsBelowMinimumReps(
        ObservedSetRecordV2[] sets,
        bool isUnilateral,
        int minReps) =>
        sets.Any(s => EffectiveReps(s, isUnilateral) < minReps);
}
