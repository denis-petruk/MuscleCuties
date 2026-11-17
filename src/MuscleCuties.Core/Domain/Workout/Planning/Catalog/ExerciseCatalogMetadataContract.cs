namespace MuscleCuties.Core.Domain.Workout.Planning.Catalog;

/// <summary>
/// Provenance of a V2 catalog field. <see cref="Verified"/> means the reviewed snapshot explicitly
/// covers it, <see cref="Inherited"/> means only the canonical seed carries it, and
/// <see cref="Missing"/> means neither source does. A gap is reported, never filled.
/// </summary>
public enum CatalogFieldStatus
{
    Verified,
    Inherited,
    Missing
}

/// <summary>Fields the V2 planning contract requires. Inventory is metadata, not policy.</summary>
public enum CatalogMetadataField
{
    Identity,
    MovementPattern,
    Equipment,
    SetupTime,
    SecondsPerRep,
    UnilateralTime,
    Contraindications,
    MuscleContribution,
    SupportedExperiences,
    ExperienceConditions,
    DemandTags,
    RestrictionAssessment,
    TimingMethods,
    Substitutions
}

public sealed record CatalogFieldAssessment(string Field, CatalogFieldStatus Status);

public sealed record ExerciseCatalogCompleteness(
    int ExerciseId,
    bool IsReviewed,
    IReadOnlyList<CatalogFieldAssessment> Fields,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> InheritedFields)
{
    public bool IsComplete => MissingFields.Count == 0;
}

/// <summary>
/// One catalog exercise as the audit sees it. The two field sets arrive pre-classified by the
/// adapter that read the sources, so Core never infers provenance from a value being non-empty.
/// </summary>
public sealed record CatalogExerciseRecord(
    int ExerciseId,
    bool IsReviewed,
    IReadOnlySet<CatalogMetadataField> VerifiedFields,
    IReadOnlySet<CatalogMetadataField> InheritedFields);

/// <summary>
/// V2 catalog completeness. Reviewing identity fields does not make an exercise V2-complete:
/// completeness is evaluated against every field the generator consumes.
/// </summary>
public static class ExerciseCatalogMetadataContract
{
    /// <summary>Required for any V2 prescription. Order is presentation order, not priority.</summary>
    public static IReadOnlyList<CatalogMetadataField> RequiredFields { get; } =
    [
        CatalogMetadataField.Identity,
        CatalogMetadataField.MovementPattern,
        CatalogMetadataField.Equipment,
        CatalogMetadataField.SetupTime,
        CatalogMetadataField.SecondsPerRep,
        CatalogMetadataField.UnilateralTime,
        CatalogMetadataField.Contraindications,
        CatalogMetadataField.MuscleContribution,
        CatalogMetadataField.SupportedExperiences,
        CatalogMetadataField.ExperienceConditions,
        CatalogMetadataField.DemandTags,
        CatalogMetadataField.RestrictionAssessment,
        CatalogMetadataField.TimingMethods,
        CatalogMetadataField.Substitutions
    ];

    /// <summary>
    /// Fields whose absence is survivable only because an approved, named planning policy covers
    /// them. They are deliberately not part of <see cref="RequiredFields"/> so a missing exercise
    /// datum can never be mistaken for a policy default.
    /// </summary>
    public static IReadOnlyList<CatalogMetadataField> PolicyCoveredFields { get; } = [];

    public static ExerciseCatalogCompleteness Evaluate(CatalogExerciseRecord? record)
    {
        if (record is null || record.ExerciseId <= 0)
            return new ExerciseCatalogCompleteness(0, false,
                RequiredFields.Select(field => new CatalogFieldAssessment(field.ToString(), CatalogFieldStatus.Missing)).ToArray(),
                RequiredFields.Select(field => field.ToString()).ToArray(), []);

        var fields = new List<CatalogFieldAssessment>(RequiredFields.Count);
        var missing = new List<string>();
        var inherited = new List<string>();

        foreach (var field in RequiredFields)
        {
            var status = record.VerifiedFields.Contains(field)
                ? CatalogFieldStatus.Verified
                : record.InheritedFields.Contains(field)
                    ? CatalogFieldStatus.Inherited
                    : CatalogFieldStatus.Missing;

            if (status == CatalogFieldStatus.Missing)
                missing.Add(field.ToString());
            if (status == CatalogFieldStatus.Inherited)
                inherited.Add(field.ToString());

            fields.Add(new CatalogFieldAssessment(field.ToString(), status));
        }

        return new ExerciseCatalogCompleteness(record.ExerciseId, record.IsReviewed, fields,
            missing.ToArray(), inherited.ToArray());
    }
}
