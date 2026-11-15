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
        Assert.Equal(59, CatalogAuditFixture.Reviews.Count);
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

    [Fact]
    public void Agreement_checks_all_six_reviewed_fields()
    {
        Assert.Equal(6, ExerciseCatalogAuditRule.AgreementFields.Count);
        Assert.Equal(
            new[]
            {
                ExerciseCatalogAuditRule.NameField,
                ExerciseCatalogAuditRule.PatternField,
                ExerciseCatalogAuditRule.EquipmentField,
                ExerciseCatalogAuditRule.SetupSecondsField,
                ExerciseCatalogAuditRule.SecondsPerRepField,
                ExerciseCatalogAuditRule.ContraindicationsField
            },
            ExerciseCatalogAuditRule.AgreementFields);
    }

    [Fact]
    public void Reviewed_snapshot_agrees_with_the_seed_on_every_agreement_field()
    {
        var divergences = CatalogAuditFixture.Reviews.Keys
            .OrderBy(id => id)
            .Select(id => (id, agreement: CatalogAuditFixture.SourceAgreement(id)))
            .Where(pair => !pair.agreement.Agrees)
            .Select(pair => $"{pair.id}:{string.Join('|', pair.agreement.DivergedFields)}")
            .ToArray();

        Assert.Empty(divergences);
    }

    [Fact]
    public void A_divergent_field_is_named_rather_than_collapsed()
    {
        var agreement = ExerciseCatalogAuditRule.CompareSources(
        [
            new CatalogSourceField(ExerciseCatalogAuditRule.NameField, "Lat Pulldown", "Lat Pulldown"),
            new CatalogSourceField(ExerciseCatalogAuditRule.SetupSecondsField, "90", "120")
        ]);

        Assert.False(agreement.Agrees);
        Assert.Equal([ExerciseCatalogAuditRule.SetupSecondsField], agreement.DivergedFields);
    }
}
