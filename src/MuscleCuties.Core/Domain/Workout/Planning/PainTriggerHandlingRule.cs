namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum PainTriggerDisposition
{
    Substituted,
    Withheld,
    SeverePainRequiresClinicalAttention
}

public sealed record PainTriggerReport(
    int ExerciseId,
    string ExerciseName,
    int PainScore,
    DateOnly ReportDate,
    PrimaryMuscleRegion AffectedRegion,
    string? UserObservation = null);

public sealed record PainTriggerAlternativeCandidate(
    int ExerciseId,
    string ExerciseName,
    PrimaryMuscleRegion Region,
    bool InvolvesProvokingPattern,
    bool IsEquipmentAvailable = true);

public sealed record PainTriggerResult(
    int OriginalExerciseId,
    PainTriggerDisposition Disposition,
    int? ReplacementExerciseId,
    string? ReplacementExerciseName,
    string ReasonCode,
    string AdvisoryText,
    bool ProvokingExerciseStopped = true);

/// <summary>
/// WP05: Pain-trigger handling and structured substitute/withhold rule.
/// Immediate response: stops provoking movement.
/// High-risk pain (>= 7): withholds and recommends qualified clinical review.
/// Tolerable/moderate pain (1-6): filters alternatives in the same region that avoid the provoking movement pattern;
/// if no safe strength alternative exists, explicitly withholds the slot (never invents auto-rehab).
/// </summary>
public static class PainTriggerHandlingRule
{
    public const int SeverePainThreshold = 7;
    public const int MildToModerateMinPain = 1;

    // Reason codes
    public const string ReasonSeverePainWithheld = "SeverePainRequiresClinicalEvaluation";
    public const string ReasonSubstitutedSafeAlternative = "PainTriggerSubstitutedSafeAlternative";
    public const string ReasonWithheldNoSafeAlternative = "PainTriggerWithheldNoPermittedStrengthAlternative";
    public const string ReasonNoPainReported = "NoPainReportedNormalExecution";

    public static PainTriggerResult Evaluate(
        PainTriggerReport report,
        IReadOnlyList<PainTriggerAlternativeCandidate>? availableCandidates)
    {
        ArgumentNullException.ThrowIfNull(report);

        // 1. If pain score is 0, normal execution
        if (report.PainScore <= 0)
        {
            return new PainTriggerResult(
                report.ExerciseId,
                PainTriggerDisposition.Substituted,
                report.ExerciseId,
                report.ExerciseName,
                ReasonNoPainReported,
                "No pain reported; exercise is safe to continue.",
                ProvokingExerciseStopped: false);
        }

        // 2. Severe pain (>= 7): Stop immediately, withhold exercise, recommend professional evaluation
        if (report.PainScore >= SeverePainThreshold)
        {
            return new PainTriggerResult(
                report.ExerciseId,
                PainTriggerDisposition.SeverePainRequiresClinicalAttention,
                ReplacementExerciseId: null,
                ReplacementExerciseName: null,
                ReasonSeverePainWithheld,
                $"Exercise '{report.ExerciseName}' stopped due to severe pain ({report.PainScore}/10). Exercise withheld; please consult a qualified clinician before resuming heavy loading.",
                ProvokingExerciseStopped: true);
        }

        // 3. Mild-to-moderate pain (1 to 6): Stop provoking exercise, search for safe alternative in the same region
        if (availableCandidates is not null && availableCandidates.Count > 0)
        {
            var eligibleAlternative = availableCandidates.FirstOrDefault(c =>
                c.ExerciseId != report.ExerciseId &&
                c.Region == report.AffectedRegion &&
                !c.InvolvesProvokingPattern &&
                c.IsEquipmentAvailable);

            if (eligibleAlternative is not null)
            {
                return new PainTriggerResult(
                    report.ExerciseId,
                    PainTriggerDisposition.Substituted,
                    eligibleAlternative.ExerciseId,
                    eligibleAlternative.ExerciseName,
                    ReasonSubstitutedSafeAlternative,
                    $"Exercise '{report.ExerciseName}' provoked pain ({report.PainScore}/10). Replaced with pain-free alternative '{eligibleAlternative.ExerciseName}'.",
                    ProvokingExerciseStopped: true);
            }
        }

        // 4. No safe alternative available: Withhold exercise with clear explanation (no fake rehab exercises)
        return new PainTriggerResult(
            report.ExerciseId,
            PainTriggerDisposition.Withheld,
            ReplacementExerciseId: null,
            ReplacementExerciseName: null,
            ReasonWithheldNoSafeAlternative,
            $"Exercise '{report.ExerciseName}' stopped due to pain ({report.PainScore}/10). Withheld because no safe strength alternative is currently available for {report.AffectedRegion}.",
            ProvokingExerciseStopped: true);
    }
}
