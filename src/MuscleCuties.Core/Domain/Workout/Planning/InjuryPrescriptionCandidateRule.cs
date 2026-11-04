namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum InjuryPrescriptionCandidateDisposition { Blocked, OriginalOptional, OriginalRequired, Alternative, Omitted }

/// <summary>Evidence must come from the trusted current canonical planning snapshot.</summary>
public sealed record InjuryPrescriptionCandidateEvidence(
    int OriginalExerciseId,
    string? OriginalSlotId,
    int? ProposedExerciseId,
    string? ProposedSlotId,
    bool EpisodeSetComplete,
    IReadOnlyCollection<InjuryRestorationEpisode>? Episodes,
    bool OriginalWasOptional,
    bool CandidateMetadataCompleteAndReviewed,
    bool CurrentRestrictionsPermit,
    bool EquipmentCompatible,
    bool DoseReviewedAndValid,
    bool NoSafeCandidateVerified,
    DateOnly EffectiveDate);

public static class InjuryPrescriptionCandidateRule
{
    public static InjuryPrescriptionCandidateDisposition Evaluate(InjuryPrescriptionCandidateEvidence evidence)
    {
        if (evidence.OriginalExerciseId <= 0 || string.IsNullOrWhiteSpace(evidence.OriginalSlotId) ||
            !evidence.EpisodeSetComplete || evidence.Episodes is null || evidence.Episodes.Count == 0 ||
            evidence.Episodes.Any(episode => episode.IsActive
                ? episode.ClearedOn is not null
                : episode.ClearedOn is null || episode.ClearedOn > evidence.EffectiveDate))
            return InjuryPrescriptionCandidateDisposition.Blocked;

        if (evidence.ProposedExerciseId is null)
            return evidence.NoSafeCandidateVerified && evidence.CandidateMetadataCompleteAndReviewed
                ? InjuryPrescriptionCandidateDisposition.Omitted : InjuryPrescriptionCandidateDisposition.Blocked;

        if (evidence.ProposedExerciseId <= 0 ||
            !string.Equals(evidence.OriginalSlotId, evidence.ProposedSlotId, StringComparison.Ordinal) ||
            !evidence.CandidateMetadataCompleteAndReviewed || !evidence.CurrentRestrictionsPermit ||
            !evidence.EquipmentCompatible || !evidence.DoseReviewedAndValid)
            return InjuryPrescriptionCandidateDisposition.Blocked;

        if (evidence.ProposedExerciseId != evidence.OriginalExerciseId)
            return InjuryPrescriptionCandidateDisposition.Alternative;

        return WorkoutInjuryClearanceRule.EvaluateRestoration(evidence.OriginalWasOptional, true,
            evidence.Episodes, evidence.EffectiveDate) switch
        {
            InjuryClearanceRestorationDisposition.RestoreOptional => InjuryPrescriptionCandidateDisposition.OriginalOptional,
            InjuryClearanceRestorationDisposition.RestoreRequired => InjuryPrescriptionCandidateDisposition.OriginalRequired,
            _ => InjuryPrescriptionCandidateDisposition.Blocked
        };
    }
}
