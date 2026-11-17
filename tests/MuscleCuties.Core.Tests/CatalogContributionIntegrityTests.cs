using MuscleCuties.Core.Domain.Workout.Planning.Catalog;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class CatalogContributionIntegrityTests
{
    [Fact]
    public void Every_active_definition_carries_contribution_records()
    {
        var missing = CatalogAuditFixture.Definitions.Keys
            .Where(id => !CatalogAuditFixture.Contributions.TryGetValue(id, out var rows) || rows.Count == 0)
            .OrderBy(id => id)
            .ToArray();

        Assert.Empty(missing);
        Assert.Equal(68, CatalogAuditFixture.Definitions.Count);
    }

    [Fact]
    public void Contribution_row_distribution_matches_the_verified_inventory()
    {
        var audit = CatalogAuditFixture.Audit;

        Assert.Equal(146, audit.TotalRows);
        Assert.Equal(73, audit.Exercises.Sum(exercise => exercise.DirectCount));
        Assert.Equal(73, audit.Exercises.Sum(exercise => exercise.IndirectCount));
        Assert.Empty(audit.InvalidFractions);
        Assert.Empty(audit.DuplicatePairs);
    }

    [Fact]
    public void Fractions_are_within_the_reviewed_range()
    {
        var invalid = CatalogAuditFixture.Contributions
            .SelectMany(pair => pair.Value.Select(row => (pair.Key, row)))
            .Where(pair => pair.row.Fraction <= 0m || pair.row.Fraction > 1m)
            .Select(pair => $"{pair.Key}:{pair.row.MuscleGroup}")
            .ToArray();

        Assert.Empty(invalid);
    }

    [Fact]
    public void Multiple_direct_contributions_are_preserved_and_reported_as_ambiguous_primary()
    {
        var audit = CatalogAuditFixture.Audit;
        var ambiguous = audit.AmbiguousPrimaries.Select(exercise => exercise.ExerciseId).OrderBy(id => id).ToArray();

        Assert.Equal(new[] { 106, 121, 123, 125, 126 }, ambiguous);
        Assert.All(audit.AmbiguousPrimaries, exercise => Assert.Equal(2, exercise.DirectCount));
        Assert.All(audit.AmbiguousPrimaries, exercise => Assert.Equal(2, exercise.MaximumGroups.Count));
    }

    [Fact]
    public void A_single_maximum_is_reported_where_it_genuinely_exists()
    {
        var backSquat = CatalogAuditFixture.Audit.Exercises.Single(exercise => exercise.ExerciseId == 121);
        var hipThrust = CatalogAuditFixture.Audit.Exercises.Single(exercise => exercise.ExerciseId == 101);

        Assert.False(backSquat.HasUniqueMaximum);
        Assert.True(hipThrust.HasUniqueMaximum);
        Assert.Equal(["Glutes"], hipThrust.MaximumGroups);
        Assert.Contains(MuscleAccountingTarget.GluteMaximus, hipThrust.DirectTargets);
    }

    [Fact]
    public void Co_reported_back_groups_collapse_so_one_working_set_is_not_banked_twice()
    {
        var audit = CatalogAuditFixture.Audit;
        var collapsed = audit.Exercises.Where(exercise => exercise.CollapsedBackGroups.Count > 1).ToArray();

        Assert.NotEmpty(collapsed);
        foreach (var exercise in collapsed)
        {
            var credits = ExerciseCatalogAuditRule.AccountingCredits(exercise);
            Assert.Equal(1.0m, credits[MuscleAccountingTarget.Back]);
        }

        var row = CatalogAuditFixture.Contributions[143];
        Assert.Contains(row, item => item.MuscleGroup == "Lats");
        Assert.Contains(row, item => item.MuscleGroup == "UpperBack");
    }

    [Fact]
    public void Pressing_never_credits_lateral_delts()
    {
        var pressing = new[] { 151, 152, 153, 154 };
        foreach (var id in pressing)
        {
            var credits = ExerciseCatalogAuditRule.AccountingCredits(
                CatalogAuditFixture.Audit.Exercises.Single(exercise => exercise.ExerciseId == id));
            Assert.False(credits.ContainsKey(MuscleAccountingTarget.LateralDelts), $"{id} credited lateral delts");
            Assert.True(credits.ContainsKey(MuscleAccountingTarget.AnteriorDelts), $"{id} credited no anterior delts");
        }
    }

    [Fact]
    public void Delt_rows_are_split_by_pattern_not_by_name()
    {
        CatalogExerciseContributionAudit Exercise(int id) =>
            CatalogAuditFixture.Audit.Exercises.Single(exercise => exercise.ExerciseId == id);

        Assert.Equal(MuscleAccountingTarget.LateralDelts,
            MuscleAccountingTaxonomy.Map("Delts", MovementPattern.LateralRaise));
        Assert.Equal(MuscleAccountingTarget.RearDelts,
            MuscleAccountingTaxonomy.Map("Delts", MovementPattern.RearDelt));
        Assert.Equal(MuscleAccountingTarget.AnteriorDelts,
            MuscleAccountingTaxonomy.Map("Delts", MovementPattern.VerticalPush));

        Assert.True(ExerciseCatalogAuditRule.AccountingCredits(Exercise(156))
            .ContainsKey(MuscleAccountingTarget.LateralDelts));
        Assert.True(ExerciseCatalogAuditRule.AccountingCredits(Exercise(158))
            .ContainsKey(MuscleAccountingTarget.RearDelts));
    }

    [Fact]
    public void Hip_abduction_stays_separate_from_gluteus_maximus_volume()
    {
        var abduction = CatalogAuditFixture.Audit.Exercises.Single(exercise => exercise.ExerciseId == 131);
        var credits = ExerciseCatalogAuditRule.AccountingCredits(abduction);

        Assert.Equal(1.0m, credits[MuscleAccountingTarget.HipAbductors]);
        Assert.Equal(0.5m, credits[MuscleAccountingTarget.GluteMaximus]);
        Assert.NotEqual(credits[MuscleAccountingTarget.HipAbductors],
            credits[MuscleAccountingTarget.GluteMaximus]);
    }

    [Fact]
    public void Indirect_contribution_is_never_promoted_to_direct()
    {
        var row = CatalogAuditFixture.Audit.Exercises.Single(exercise => exercise.ExerciseId == 111);
        var credits = ExerciseCatalogAuditRule.AccountingCredits(row);

        Assert.Equal(1.0m, credits[MuscleAccountingTarget.Hamstrings]);
        Assert.Equal(0.5m, credits[MuscleAccountingTarget.GluteMaximus]);
        Assert.DoesNotContain(MuscleAccountingTarget.GluteMaximus, row.DirectTargets);
    }

    [Fact]
    public void Duplicate_exercise_muscle_rows_are_identifiable()
    {
        var audit = ExerciseCatalogAuditRule.AnalyzeContributions(
        [
            new CatalogExerciseContributionAuditInput(1, "Synthetic", true, MovementPattern.HipThrust,
            [
                new CatalogMuscleRow(1, "Glutes", 1.0m),
                new CatalogMuscleRow(1, "Glutes", 0.5m)
            ])
        ]);

        Assert.Single(audit.DuplicatePairs);
        Assert.Contains("1:Glutes", audit.DuplicatePairs);
    }
}
