namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ReviewedCandidateEligibility { Unknown, Eligible, Ineligible }
public enum ReviewedCandidateSelectionStatus { Blocked, OriginalOptional, OriginalRequired, Alternative, Omitted }

public sealed record InjuryReviewScope(string CatalogRevision, string PolicyRevision, string TemplateId,
    string TemplateVersion, int SessionIndex, string SlotId, int InjurySiteFlag, string ProgressionPhase);
public sealed record ReviewedInjuryCandidate(int ExerciseId, ReviewedCandidateEligibility Eligibility,
    int MinimumSets, int MaximumSets, int MinimumRepetitions, int MaximumRepetitions,
    bool LoadEvidenceRequired, decimal? MaximumLoadKg);
public sealed record ReviewedInjuryScopeEvidence(InjuryReviewScope Scope, string CandidateUniverseId,
    bool UniverseVerified, IReadOnlyCollection<ReviewedInjuryCandidate> Candidates);
public sealed record InjuryCandidatePrescription(int ExerciseId, int Sets, int Repetitions,
    decimal? LoadKg, bool LoadProvenanceConfirmed, bool RestrictionsAndEquipmentEvidenceComplete,
    bool CurrentRestrictionsPermit, bool EquipmentCompatible);
public sealed record ReviewedInjurySelectionInput(int OriginalExerciseId, bool OriginalWasOptional,
    DateOnly EffectiveDate, IReadOnlyCollection<InjuryRestorationEpisode> Episodes,
    bool EpisodeSetComplete, bool ScopeEpisodeBindingsVerified, IReadOnlyCollection<InjuryReviewScope> RequiredScopes,
    string CandidateUniverseId, IReadOnlyCollection<int> CandidateUniverse,
    IReadOnlyCollection<ReviewedInjuryScopeEvidence> Reviews,
    IReadOnlyCollection<InjuryCandidatePrescription> Prescriptions);
public sealed record ReviewedInjurySelectionResult(ReviewedCandidateSelectionStatus Status, int? ExerciseId);

/// <summary>Requires independently verified, versioned review and complete candidate coverage for every episode scope.</summary>
public static class ReviewedInjuryCandidateSelectionRule
{
    public static ReviewedInjurySelectionResult Evaluate(ReviewedInjurySelectionInput input)
    {
        var blocked = new ReviewedInjurySelectionResult(ReviewedCandidateSelectionStatus.Blocked, null);
        if (!input.EpisodeSetComplete || !input.ScopeEpisodeBindingsVerified || input.Episodes.Count == 0 || input.RequiredScopes.Count == 0 ||
            input.RequiredScopes.Count != input.Episodes.Count || input.RequiredScopes.Distinct().Count() != input.RequiredScopes.Count ||
            string.IsNullOrWhiteSpace(input.CandidateUniverseId) || input.CandidateUniverse.Count == 0 ||
            input.CandidateUniverse.Any(id => id <= 0) || input.CandidateUniverse.Distinct().Count() != input.CandidateUniverse.Count ||
            !input.CandidateUniverse.Contains(input.OriginalExerciseId) ||
            input.Prescriptions.Select(p => p.ExerciseId).Distinct().Count() != input.Prescriptions.Count ||
            input.Reviews.Count != input.RequiredScopes.Count || input.Reviews.Select(r => r.Scope).Distinct().Count() != input.Reviews.Count)
            return blocked;
        var universe = input.CandidateUniverse.ToHashSet();
        if (input.RequiredScopes.Any(scope => string.IsNullOrWhiteSpace(scope.CatalogRevision) ||
                string.IsNullOrWhiteSpace(scope.PolicyRevision) || string.IsNullOrWhiteSpace(scope.TemplateId) ||
                string.IsNullOrWhiteSpace(scope.TemplateVersion) || string.IsNullOrWhiteSpace(scope.SlotId) ||
                string.IsNullOrWhiteSpace(scope.ProgressionPhase) || scope.InjurySiteFlag <= 0 || scope.SessionIndex < 0) ||
            input.Episodes.Any(episode => episode.IsActive ? episode.ClearedOn is not null :
                episode.ClearedOn is null || episode.ClearedOn > input.EffectiveDate)) return blocked;
        foreach (var scope in input.RequiredScopes)
        {
            var review = input.Reviews.SingleOrDefault(row => row.Scope == scope);
            if (review is null || !review.UniverseVerified || review.CandidateUniverseId != input.CandidateUniverseId ||
                review.Candidates.Count != universe.Count || review.Candidates.Select(row => row.ExerciseId).Distinct().Count() != universe.Count ||
                !review.Candidates.Select(row => row.ExerciseId).ToHashSet().SetEquals(universe)) return blocked;
        }
        var eligible = new List<int>();
        foreach (var id in universe.Order())
        {
            var reviewed = input.Reviews.Select(review => review.Candidates.Single(row => row.ExerciseId == id)).ToArray();
            if (reviewed.Any(row => row.Eligibility == ReviewedCandidateEligibility.Unknown ||
                !Enum.IsDefined(row.Eligibility))) return blocked;
            if (reviewed.Any(row => row.Eligibility == ReviewedCandidateEligibility.Ineligible)) continue;
            var prescription = input.Prescriptions.SingleOrDefault(row => row.ExerciseId == id);
            if (prescription is null) return blocked;
            if (!prescription.RestrictionsAndEquipmentEvidenceComplete) return blocked;
            if (!prescription.CurrentRestrictionsPermit || !prescription.EquipmentCompatible) continue;
            if (reviewed.Any(row => row.MinimumSets <= 0 || row.MaximumSets < row.MinimumSets ||
                row.MinimumRepetitions <= 0 || row.MaximumRepetitions < row.MinimumRepetitions ||
                (row.LoadEvidenceRequired && (row.MaximumLoadKg is null || row.MaximumLoadKg <= 0 ||
                    !prescription.LoadProvenanceConfirmed || prescription.LoadKg is null || prescription.LoadKg < 0)))) return blocked;
            if (reviewed.Any(row => prescription.Sets < row.MinimumSets || prescription.Sets > row.MaximumSets ||
                prescription.Repetitions < row.MinimumRepetitions || prescription.Repetitions > row.MaximumRepetitions ||
                (row.LoadEvidenceRequired && prescription.LoadKg > row.MaximumLoadKg))) continue;
            eligible.Add(id);
        }
        if (eligible.Contains(input.OriginalExerciseId))
        {
            var restored = WorkoutInjuryClearanceRule.EvaluateRestoration(input.OriginalWasOptional, true, input.Episodes, input.EffectiveDate);
            if (restored == InjuryClearanceRestorationDisposition.RestoreOptional)
                return new(ReviewedCandidateSelectionStatus.OriginalOptional, input.OriginalExerciseId);
            if (restored == InjuryClearanceRestorationDisposition.RestoreRequired)
                return new(ReviewedCandidateSelectionStatus.OriginalRequired, input.OriginalExerciseId);
        }
        var alternative = eligible.FirstOrDefault(id => id != input.OriginalExerciseId);
        return alternative > 0 ? new(ReviewedCandidateSelectionStatus.Alternative, alternative) :
            eligible.Count == 0 ? new(ReviewedCandidateSelectionStatus.Omitted, null) : blocked;
    }
}
