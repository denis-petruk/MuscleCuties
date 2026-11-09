using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Planner facts for one in-session swap. The values come from the same saved profile, injury
/// answers and reviewed catalog the preview uses; an unknown equipment profile stays unknown.
/// </summary>
public sealed record ExerciseSwapEligibilityInput(
    int OriginalExerciseId,
    IReadOnlyList<StrengthExerciseCatalogEntry>? Catalog,
    Equipment? Equipment,
    ActivityExperience Experience,
    IReadOnlySet<int>? ExcludedExerciseIds,
    IReadOnlySet<MovementPattern>? BlockedMovementPatterns,
    InjuryFlag ActiveInjuryFlags,
    bool UnknownExperienceAssessmentCompleted = false);

/// <summary>A reviewed close alternative and the review conditions that travel with it.</summary>
public sealed record ExerciseSwapCandidate(StrengthExerciseCatalogEntry Entry, string Conditions);

public sealed record ExerciseSwapEligibilityResult(
    IReadOnlyList<ExerciseSwapCandidate> Candidates,
    IReadOnlyList<string> ReasonCodes)
{
    public bool Allows(int exerciseId) =>
        Candidates.Any(candidate => candidate.Entry.Candidate.ExerciseId == exerciseId);
}

/// <summary>
/// In-session exercise swaps use the planner's hard eligibility filters and only reviewed
/// directed substitutions. Automatic substitutions stay disabled: the user's explicit choice of a
/// listed close alternative is what acknowledges that pair's review conditions, and nothing else
/// (manual-programming pairs, rejected pairs, unreviewed pairs) is ever offered.
/// </summary>
public static class ExerciseSwapEligibilityRule
{
    public const string EquipmentProfileMissingCode = "SwapEquipmentProfileMissing";
    public const string OriginalExerciseNotReviewedCode = "SwapOriginalExerciseNotReviewed";
    public const string NoEligibleReviewedSubstituteCode = "SwapNoEligibleReviewedSubstitute";
    public const string SubstituteNotEligibleCode = "SwapSubstituteNotEligible";

    private static readonly IReadOnlySet<int> NoIds = new HashSet<int>();
    private static readonly IReadOnlySet<MovementPattern> NoPatterns = new HashSet<MovementPattern>();
    private static readonly IReadOnlySet<ActivityDemandTag> NoDemandTags = new HashSet<ActivityDemandTag>();

    public static ExerciseSwapEligibilityResult Evaluate(ExerciseSwapEligibilityInput? input)
    {
        if (input?.Equipment is not { } equipment || !Enum.IsDefined(equipment))
            return Empty(EquipmentProfileMissingCode);

        var catalog = input.Catalog ?? [];
        var original = catalog.FirstOrDefault(entry => entry?.Candidate?.ExerciseId == input.OriginalExerciseId);
        if (original is null || original.MetadataGaps is not { Count: 0 } ||
            !original.Candidate.ReviewEvidencePresent ||
            original.Candidate.DirectedSubstitutionReviews is not { } reviews)
            return Empty(OriginalExerciseNotReviewedCode);

        var closeAlternatives = reviews
            .Where(review => review is not null &&
                             review.Category == DirectedSubstitutionCategory.CloseProgrammingAlternative &&
                             review.TargetExerciseId != input.OriginalExerciseId &&
                             !string.IsNullOrWhiteSpace(review.Conditions))
            .GroupBy(review => review.TargetExerciseId)
            .Select(group => group.First())
            .OrderBy(review => review.TargetExerciseId)
            .ToArray();
        var context = new ExerciseEligibilityContext(
            StrengthPlanPreviewRule.AvailableEquipmentFor(equipment),
            input.Experience,
            input.ExcludedExerciseIds ?? NoIds,
            input.BlockedMovementPatterns ?? NoPatterns,
            NoDemandTags,
            HasBodyZoneLimitation: false,
            input.ActiveInjuryFlags,
            input.UnknownExperienceAssessmentCompleted,
            closeAlternatives.Select(review => PairKey(input.OriginalExerciseId, review.TargetExerciseId))
                .ToHashSet(StringComparer.Ordinal));

        var candidates = new List<ExerciseSwapCandidate>();
        foreach (var review in closeAlternatives)
        {
            var substitute = catalog.FirstOrDefault(entry => entry?.Candidate?.ExerciseId == review.TargetExerciseId);
            // The swap keeps the slot's sets and repetitions, so only complete straight-set entries qualify.
            if (substitute is null || substitute.MetadataGaps is not { Count: 0 } ||
                substitute.Candidate.SupportedTimingMethods?.Contains(ExerciseTimingMethod.StraightSets) != true)
                continue;
            var decision = ExerciseEligibilityRule.EvaluateSubstitution(original.Candidate, substitute.Candidate,
                context);
            if (decision.Status == ExerciseEligibilityStatus.Eligible)
                candidates.Add(new ExerciseSwapCandidate(substitute, review.Conditions));
        }

        return candidates.Count == 0
            ? Empty(NoEligibleReviewedSubstituteCode)
            : new ExerciseSwapEligibilityResult(candidates, []);
    }

    /// <summary>The swap path's experience: identical to the planner for the same inputs.</summary>
    public static ActivityExperience Experience(WorkoutActivityType strengthActivity,
        ActivityTrainingHistory? history, DateOnly localDate, TrainingExperienceLevel? profileExperience) =>
        StrengthPlanPreviewRule.EligibilityExperience(strengthActivity, history, localDate, profileExperience);

    private static string PairKey(int originalExerciseId, int substituteExerciseId) =>
        $"{originalExerciseId}:{substituteExerciseId}";

    private static ExerciseSwapEligibilityResult Empty(string reason) => new([], [reason]);
}
