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

}
