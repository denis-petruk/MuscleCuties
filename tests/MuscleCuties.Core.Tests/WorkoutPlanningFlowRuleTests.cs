using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPlanningFlowRuleTests
{
    [Theory]
    [InlineData(false, WorkoutPlanningSuccessDestination.Dashboard)]
    [InlineData(true, WorkoutPlanningSuccessDestination.Workout)]
    public void FirstAndReplacementApplicationsNavigateToTheirOwnDestinations(
        bool replacesExistingPlan,
        WorkoutPlanningSuccessDestination expectedDestination)
    {
        var destination = WorkoutPlanningFlowRule.Success(replacesExistingPlan);
        Assert.Equal(expectedDestination, destination);
    }

    [Theory]
    [InlineData(true, false, WorkoutPlanningBackAction.ClosePreview)]
    [InlineData(false, false, WorkoutPlanningBackAction.Leave)]
    [InlineData(false, true, WorkoutPlanningBackAction.ResumeAfterApply)]
    [InlineData(true, true, WorkoutPlanningBackAction.ResumeAfterApply)]
    public void BackClosesPreviewBeforeLeavingGeneration(
        bool hasPreview,
        bool navigationPending,
        WorkoutPlanningBackAction expectedAction)
    {
        var action = WorkoutPlanningFlowRule.Back(hasPreview, navigationPending);
        Assert.Equal(expectedAction, action);
    }

    [Theory]
    [InlineData(false, false, false, true, true, true, true, true, true)]
    [InlineData(false, false, false, true, true, true, true, false, false)]
    [InlineData(false, false, false, false, false, true, true, true, false)]
    [InlineData(true, false, false, false, true, true, true, true, false)]
    [InlineData(false, true, false, false, true, true, true, true, false)]
    [InlineData(false, false, true, false, true, true, true, true, false)]
    [InlineData(false, false, false, false, true, false, true, true, false)]
    [InlineData(false, false, false, false, true, true, false, true, false)]
    public void UncertainAcceptanceCanRetryButInvalidatedNewAcceptanceCannot(
        bool busy,
        bool accepted,
        bool navigationPending,
        bool invalidated,
        bool persisted,
        bool hasLease,
        bool feasible,
        bool uncertainAcceptance,
        bool expectedCanAccept)
    {
        var canAccept = WorkoutPlanningFlowRule.CanAccept(
            busy,
            accepted,
            navigationPending,
            invalidated,
            persisted,
            hasLease,
            feasible,
            uncertainAcceptance);

        Assert.Equal(expectedCanAccept, canAccept);
    }

    [Theory]
    [InlineData(false, true, true, false, false, false, false, true)]
    [InlineData(true, true, true, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false)]
    [InlineData(false, true, true, true, false, false, false, false)]
    [InlineData(false, true, true, false, true, false, false, false)]
    [InlineData(false, true, true, false, false, true, false, false)]
    [InlineData(false, true, true, false, false, false, true, false)]
    public void FirstPlanAutoApplyRequiresCompleteSafeUnreducedPersistedProposal(
        bool replacesExistingPlan,
        bool feasible,
        bool persisted,
        bool missingRequiredAnswers,
        bool styleChoiceRequired,
        bool hasOmissions,
        bool reducedVolume,
        bool expectedCanAutoApply)
    {
        var canAutoApply = WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan,
            feasible,
            persisted,
            missingRequiredAnswers,
            styleChoiceRequired,
            hasOmissions,
            reducedVolume);

        Assert.Equal(expectedCanAutoApply, canAutoApply);
    }
}
