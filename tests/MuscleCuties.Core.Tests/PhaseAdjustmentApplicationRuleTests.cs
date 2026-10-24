using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class PhaseAdjustmentApplicationRuleTests
{
    private const int AcceptedAccessorySets = 3;
    private const int ExpectedAccessorySets = 2;
    private const int ExerciseId = 101;
    private const string ExistingGentleTemplateId = "reviewed-gentle-template";
    private static readonly DateOnly Today = new(2026, 10, 24);
    private static readonly PhaseBaseline LighterBaseline = new(
        PhaseAdjustmentRule.LighterDiscomfortRating,
        PhaseAdjustmentRule.LighterEnergyRating);

    [Fact]
    public void FieldChangesAreNotAppliedUntilUserConfirms()
    {
        var accepted = AccessorySession();
        var preview = Preview(accepted);

        var result = PhaseAdjustmentRule.Apply(Gate(accepted.SessionId), preview,
            [accepted], [], applyChanges: false);

        Assert.Equal(PhaseAdjustmentApplicationStatus.NotApplied, result.Status);
        Assert.Equal(AcceptedAccessorySets, Assert.Single(result.Sessions).Exercises.Single().AcceptedSets);
        Assert.Equal("UserDidNotApplyChanges", result.LimitationCode);
    }

    [Fact]
    public void ExplicitConfirmationAppliesOnlyThePreviewedAccessorySetChange()
    {
        var accepted = AccessorySession();
        var result = PhaseAdjustmentRule.Apply(Gate(accepted.SessionId), Preview(accepted),
            [accepted], [], applyChanges: true);

        Assert.Equal(PhaseAdjustmentApplicationStatus.Applied, result.Status);
        Assert.Equal(ExpectedAccessorySets, Assert.Single(result.Sessions).Exercises.Single().AcceptedSets);
        Assert.True(result.RequiresStrengthDurationRecalculation);
    }

    [Fact]
    public void HiitAlternativeMustMatchAnExistingEligibleTemplate()
    {
        var accepted = new PhaseAdjustmentSession(Guid.NewGuid(), ActivityModality.Other,
            SessionTrainingStyle.Hiit, [], null,
        [new PhaseAdjustmentTemplateOption(
            PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate,
            ExistingGentleTemplateId)]);
        var preview = Preview(accepted);
        var selection = new PhaseAdjustmentChoiceSelection(accepted.SessionId,
            PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate,
            ExistingGentleTemplateId);

        var result = PhaseAdjustmentRule.Apply(Gate(accepted.SessionId), preview,
            [accepted], [selection], applyChanges: true);

        Assert.Equal(PhaseAdjustmentApplicationStatus.Applied, result.Status);
        Assert.Equal(ExistingGentleTemplateId,
            Assert.Single(result.Sessions).SelectedReplacementTemplateId);
    }

    [Fact]
    public void RejectedOrMissingAlternativeTemplateCannotChangeTheAcceptedSession()
    {
        var accepted = new PhaseAdjustmentSession(Guid.NewGuid(), ActivityModality.Other,
            SessionTrainingStyle.Hiit, [], null);
        var preview = Preview(accepted);
        var selection = new PhaseAdjustmentChoiceSelection(accepted.SessionId,
            PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate,
            ExistingGentleTemplateId);

        var result = PhaseAdjustmentRule.Apply(Gate(accepted.SessionId), preview,
            [accepted], [selection], applyChanges: true);

        Assert.Equal(PhaseAdjustmentApplicationStatus.InvalidInput, result.Status);
        Assert.Null(Assert.Single(result.Sessions).SelectedReplacementTemplateId);
        Assert.Equal("PhaseAdjustmentChoiceNotOffered", result.LimitationCode);
    }

    private static PhaseAdjustmentSession AccessorySession() =>
        new(Guid.NewGuid(), ActivityModality.Strength, SessionTrainingStyle.General,
        [new PhaseAdjustmentExercise(Guid.NewGuid(), ExerciseId,
            AcceptedAccessorySets, IsOptional: true, IsAccessory: true)], null);

    private static PhaseAdjustmentPreview Preview(PhaseAdjustmentSession session) =>
        PhaseAdjustmentRule.Preview(Gate(session.SessionId), LighterBaseline,
            PhaseBaselineScale.QuizMappedOneToFive, [session]);

    private static PhaseReviewGateResult Gate(Guid sessionId) => new(
        PhaseReviewGateStatus.ReadyForAdjustmentPreview,
        Today,
        Today.AddDays(PhaseReviewRule.FollowingCalendarDates),
        [sessionId],
        null);
}
