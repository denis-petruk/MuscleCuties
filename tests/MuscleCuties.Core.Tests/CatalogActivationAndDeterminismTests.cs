using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.Catalog;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class CatalogActivationAndDeterminismTests
{
    [Fact]
    public void Unreviewed_rock_climbing_stays_inactive_in_every_context()
    {
        var input = CatalogAuditFixture.ActivationInput(191);

        foreach (var context in Enum.GetValues<ExerciseUseContext>())
        {
            var decision = ExerciseCatalogActivationRule.Evaluate(input, context, ActivityExperience.Beginner);
            Assert.False(decision.IsActive, $"191 active in {context}");
            Assert.Contains(ExerciseCatalogActivationRule.UnreviewedCode, decision.BlockingCodes);
        }
    }

    [Fact]
    public void Timed_only_movement_is_context_specific_rather_than_globally_deactivated()
    {
        var input = CatalogAuditFixture.ActivationInput(136);

        var repetition = ExerciseCatalogActivationRule.Evaluate(input, ExerciseUseContext.RepetitionStrength,
            ActivityExperience.Beginner);
        var timed = ExerciseCatalogActivationRule.Evaluate(input, ExerciseUseContext.TimedStrength,
            ActivityExperience.Beginner);

        Assert.False(repetition.IsActive);
        Assert.Contains(ExerciseCatalogActivationRule.TimingContextUnsupportedCode, repetition.BlockingCodes);
        Assert.True(timed.IsActive);
    }

    [Fact]
    public void Static_core_is_flagged_but_not_deactivated()
    {
        var input = CatalogAuditFixture.ActivationInput(174);
        var decision = ExerciseCatalogActivationRule.Evaluate(input, ExerciseUseContext.TimedStrength,
            ActivityExperience.Beginner);

        Assert.True(decision.IsActive);
        Assert.Contains(ExerciseCatalogActivationRule.StaticCoreAdvisory, decision.AdvisoryCodes);
    }

    [Fact]
    public void Established_only_exercise_requires_exercise_specific_familiarity_not_a_blanket_ban()
    {
        var input = CatalogAuditFixture.ActivationInput(112);
        var decision = ExerciseCatalogActivationRule.Evaluate(input, ExerciseUseContext.RepetitionStrength,
            ActivityExperience.Beginner);

        Assert.True(decision.RequiresExerciseSpecificFamiliarity);
        Assert.Contains(ExerciseCatalogActivationRule.FamiliarityRequiredCode, decision.AdvisoryCodes);
        Assert.True(decision.IsActive);
    }

    [Fact]
    public void Reviewed_and_complete_exercise_is_active_for_repetition_strength()
    {
        var decision = ExerciseCatalogActivationRule.Evaluate(CatalogAuditFixture.ActivationInput(101),
            ExerciseUseContext.RepetitionStrength, ActivityExperience.Beginner);

        Assert.True(decision.IsActive);
        Assert.Empty(decision.BlockingCodes);
    }

    [Fact]
    public void Missing_review_blocks_aerobic_context_for_an_unreviewed_definition()
    {
        var decision = ExerciseCatalogActivationRule.Evaluate(CatalogAuditFixture.ActivationInput(191),
            ExerciseUseContext.AerobicOrSkill, ActivityExperience.Established);

        Assert.False(decision.IsActive);
    }

    [Fact]
    public void Candidate_document_is_byte_identical_across_repeated_generation()
    {
        var first = CatalogAuditFixture.CandidateDocument();
        var second = CatalogAuditFixture.CandidateDocument();

        Assert.Equal(first, second);
        Assert.DoesNotContain("\r", first);
        Assert.Contains(CandidateCatalogMetadataDocument.SchemaVersion, first);
        Assert.Contains(CandidateCatalogMetadataDocument.CandidateStatus, first);
    }

    [Fact]
    public void Candidate_document_carries_no_generated_timestamp()
    {
        var document = CatalogAuditFixture.CandidateDocument();

        Assert.DoesNotContain("generatedAt", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UtcNow", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DateTime", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Candidate_document_is_ordered_by_exercise_and_target()
    {
        var document = CatalogAuditFixture.CandidateDocument();
        var first = document.IndexOf("\"exerciseId\": 101", StringComparison.Ordinal);
        var second = document.IndexOf("\"exerciseId\": 102", StringComparison.Ordinal);
        var last = document.IndexOf("\"exerciseId\": 191", StringComparison.Ordinal);

        Assert.True(first > 0);
        Assert.True(first < second);
        Assert.True(second < last);
        Assert.Equal(68, CatalogAuditFixture.Audit.Exercises.Count);
    }
}
