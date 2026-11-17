using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.Catalog;

/// <summary>Where an exercise is being considered. Eligibility is per context, never global.</summary>
public enum ExerciseUseContext
{
    RepetitionStrength,
    TimedStrength,
    AerobicOrSkill
}

public sealed record CatalogActivationInput(
    CatalogExerciseRecord Record,
    ExerciseCatalogCompleteness Completeness,
    CatalogExerciseContributionAudit? Contribution,
    IReadOnlySet<ExerciseTimingMethod>? TimingMethods,
    IReadOnlySet<ActivityExperience>? SupportedExperiences,
    MovementPattern Pattern,
    bool HasReviewedAerobicContext = false);

public sealed record CatalogActivationDecision(
    int ExerciseId,
    ExerciseUseContext Context,
    bool IsActive,
    IReadOnlyList<string> BlockingCodes,
    IReadOnlyList<string> AdvisoryCodes,
    bool RequiresExerciseSpecificFamiliarity);

/// <summary>
/// V2 activation predicate. An entry is active only for a context whose evidence it actually has:
/// reviewed metadata, complete V2 fields, matching timing, and contribution records. Timed-only
/// movements stay usable for timed work instead of being deactivated everywhere, and an exercise
/// label never becomes a blanket experience prohibition.
/// </summary>
public static class ExerciseCatalogActivationRule
{
    public const string UnreviewedCode = "CatalogMetadataUnreviewed";
    public const string MissingContributionCode = "CatalogContributionRecordsMissing";
    public const string TimingContextUnsupportedCode = "TimingContextUnsupported";
    public const string IncompletePrefix = "CatalogFieldMissing:";
    public const string FamiliarityRequiredCode = "ExerciseSpecificFamiliarityRequired";
    public const string StaticCoreAdvisory = "StaticCorePrefersDynamic";

    private static readonly HashSet<MovementPattern> StaticCorePatterns = new HashSet<MovementPattern>
    {
        MovementPattern.AntiExtension,
        MovementPattern.AntiRotation,
        MovementPattern.AntiLateralFlexion
    };

    public static CatalogActivationDecision Evaluate(CatalogActivationInput? input, ExerciseUseContext context,
        ActivityExperience experience)
    {
        if (input is null)
            return new CatalogActivationDecision(0, context, false, [UnreviewedCode], [], false);

        var blocking = new List<string>();
        var advisories = new List<string>();

        if (!input.Record.IsReviewed)
            blocking.Add(UnreviewedCode);

        if (!input.Completeness.IsComplete)
            blocking.AddRange(input.Completeness.MissingFields.Select(field => $"{IncompletePrefix}{field}"));

        var timing = input.TimingMethods ?? new HashSet<ExerciseTimingMethod>();
        var supportsStraightSets = timing.Contains(ExerciseTimingMethod.StraightSets);
        var supportsTimed = timing.Contains(ExerciseTimingMethod.Timed);
        var contextTimingSupported = context switch
        {
            ExerciseUseContext.RepetitionStrength => supportsStraightSets,
            ExerciseUseContext.TimedStrength => supportsStraightSets || supportsTimed,
            ExerciseUseContext.AerobicOrSkill => true,
            _ => false
        };
        if (!contextTimingSupported)
            blocking.Add(TimingContextUnsupportedCode);

        if (input.Contribution is null || input.Contribution.Rows.Count == 0)
            blocking.Add(MissingContributionCode);

        if (context == ExerciseUseContext.AerobicOrSkill && !input.HasReviewedAerobicContext)
            blocking.Add(UnreviewedCode);

        var requiresFamiliarity = input.SupportedExperiences is { Count: > 0 } supported &&
                                  !supported.Contains(experience);
        if (requiresFamiliarity)
            advisories.Add(FamiliarityRequiredCode);

        if (StaticCorePatterns.Contains(input.Pattern))
            advisories.Add(StaticCoreAdvisory);

        return new CatalogActivationDecision(input.Record.ExerciseId, context,
            blocking.Count == 0,
            blocking.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray(),
            advisories.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray(),
            requiresFamiliarity);
    }
}
