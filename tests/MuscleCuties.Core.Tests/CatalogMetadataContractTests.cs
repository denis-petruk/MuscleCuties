using MuscleCuties.Core.Domain.Workout.Planning.Catalog;

namespace MuscleCuties.Core.Tests;

public sealed class CatalogMetadataContractTests
{
    [Fact]
    public void Every_reviewed_exercise_satisfies_the_required_v2_contract()
    {
        var incomplete = CatalogAuditFixture.Records
            .Where(record => CatalogAuditFixture.Reviews.ContainsKey(record.ExerciseId))
            .Select(ExerciseCatalogMetadataContract.Evaluate)
            .Where(completeness => !completeness.IsComplete)
            .Select(completeness => $"{completeness.ExerciseId}:{string.Join('|', completeness.MissingFields)}")
            .ToArray();

        Assert.Empty(incomplete);
        Assert.Equal(67, CatalogAuditFixture.Reviews.Count);
    }

    [Fact]
    public void Unreviewed_definition_is_never_v2_complete()
    {
        var completeness = ExerciseCatalogMetadataContract.Evaluate(CatalogAuditFixture.Record(191));

        Assert.False(completeness.IsComplete);
        Assert.False(completeness.IsReviewed);
        Assert.Contains(nameof(CatalogMetadataField.Identity), completeness.MissingFields);
        Assert.Contains(nameof(CatalogMetadataField.Equipment), completeness.MissingFields);
    }

    [Fact]
    public void Reviewing_identity_fields_alone_does_not_make_an_exercise_complete()
    {
        var record = new CatalogExerciseRecord(999, true,
            new HashSet<CatalogMetadataField>
            {
                CatalogMetadataField.Identity,
                CatalogMetadataField.MovementPattern
            },
            new HashSet<CatalogMetadataField>());

        var completeness = ExerciseCatalogMetadataContract.Evaluate(record);

        Assert.False(completeness.IsComplete);
        Assert.Contains(nameof(CatalogMetadataField.MuscleContribution), completeness.MissingFields);
        Assert.Contains(nameof(CatalogMetadataField.Contraindications), completeness.MissingFields);
        Assert.Contains(nameof(CatalogMetadataField.TimingMethods), completeness.MissingFields);
    }

    [Fact]
    public void Inherited_field_is_reported_as_inherited_not_verified()
    {
        var completeness = ExerciseCatalogMetadataContract.Evaluate(CatalogAuditFixture.Record(101));

        var muscle = completeness.Fields.Single(field => field.Field == nameof(CatalogMetadataField.MuscleContribution));
        var equipment = completeness.Fields.Single(field => field.Field == nameof(CatalogMetadataField.Equipment));

        Assert.Equal(CatalogFieldStatus.Inherited, muscle.Status);
        Assert.Equal(CatalogFieldStatus.Verified, equipment.Status);
        Assert.Contains(nameof(CatalogMetadataField.MuscleContribution), completeness.InheritedFields);
    }

    [Fact]
    public void Missing_exercise_metadata_is_never_supplied_as_a_default()
    {
        var completeness = ExerciseCatalogMetadataContract.Evaluate(new CatalogExerciseRecord(7, false,
            new HashSet<CatalogMetadataField>(), new HashSet<CatalogMetadataField>()));

        Assert.False(completeness.IsComplete);
        Assert.Equal(ExerciseCatalogMetadataContract.RequiredFields.Count, completeness.MissingFields.Count);
        Assert.All(completeness.Fields, field => Assert.Equal(CatalogFieldStatus.Missing, field.Status));
    }

}
