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
    [InlineData(false, true, true, false, false, false, true)]
    [InlineData(true, true, true, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false)]
    [InlineData(false, true, true, true, false, false, false)]
    [InlineData(false, true, true, false, true, false, false)]
    [InlineData(false, true, true, false, false, true, false)]
    public void FirstPlanAutoApplyRequiresCompleteFeasiblePersistedProposal(
        bool replacesExistingPlan,
        bool feasible,
        bool persisted,
        bool missingRequiredAnswers,
        bool styleChoiceRequired,
        bool hasOmissions,
        bool expectedCanAutoApply)
    {
        var canAutoApply = WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan,
            feasible,
            persisted,
            missingRequiredAnswers,
            styleChoiceRequired,
            hasOmissions);

        Assert.Equal(expectedCanAutoApply, canAutoApply);
    }

    [Fact]
    public void FirstPlanAutoApplyDoesNotRequireReducedVolumeAcknowledgment()
    {
        Assert.True(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: false,
            feasible: true,
            persisted: true,
            missingRequiredAnswers: false,
            styleChoiceRequired: false,
            hasOmissions: false));

        var acceptance = WorkoutPlanningFlowRule.AcceptanceAcknowledgments(
            replacesExistingPlan: false,
            reducedVolumeRequired: true);

        Assert.True(acceptance.ReducedVolumeAcknowledged);
    }

    [Fact]
    public void AcceptanceAcknowledgments_CarryReplacementAndReducedVolume()
    {
        var acceptance = WorkoutPlanningFlowRule.AcceptanceAcknowledgments(
            replacesExistingPlan: true,
            reducedVolumeRequired: true);

        Assert.True(acceptance.ReplacementConfirmed);
        Assert.True(acceptance.ReducedVolumeAcknowledged);
    }

    [Fact]
    public void AcceptanceAcknowledgments_WithoutDecisions_KeepFlagsClear()
    {
        var acceptance = WorkoutPlanningFlowRule.AcceptanceAcknowledgments(false, false);

        Assert.False(acceptance.ReplacementConfirmed);
        Assert.False(acceptance.ReducedVolumeAcknowledged);
    }

    [Theory]
    [InlineData(WorkoutPlanningState.Initial, WorkoutPlanningEvent.PreviewLoaded, WorkoutPlanningState.PreviewReady)]
    [InlineData(WorkoutPlanningState.PreviewReady, WorkoutPlanningEvent.StartAccepting, WorkoutPlanningState.Accepting)]
    [InlineData(WorkoutPlanningState.PreviewReady, WorkoutPlanningEvent.InvalidateInputs, WorkoutPlanningState.Invalidated)]
    [InlineData(WorkoutPlanningState.PreviewReady, WorkoutPlanningEvent.ClosePreview, WorkoutPlanningState.Initial)]
    [InlineData(WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptSucceeded, WorkoutPlanningState.Accepted)]
    [InlineData(WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptTimedOutOrNetworkFailed, WorkoutPlanningState.UncertainAcceptance)]
    [InlineData(WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptConflictOrStale, WorkoutPlanningState.Invalidated)]
    [InlineData(WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptValidationFailed, WorkoutPlanningState.PreviewReady)]
    [InlineData(WorkoutPlanningState.UncertainAcceptance, WorkoutPlanningEvent.StartAccepting, WorkoutPlanningState.Accepting)]
    [InlineData(WorkoutPlanningState.UncertainAcceptance, WorkoutPlanningEvent.InvalidateInputs, WorkoutPlanningState.Invalidated)]
    [InlineData(WorkoutPlanningState.UncertainAcceptance, WorkoutPlanningEvent.ClosePreview, WorkoutPlanningState.Initial)]
    [InlineData(WorkoutPlanningState.Accepted, WorkoutPlanningEvent.NavigateStarted, WorkoutPlanningState.NavigationPending)]
    [InlineData(WorkoutPlanningState.NavigationPending, WorkoutPlanningEvent.NavigateCompleted, WorkoutPlanningState.Completed)]
    [InlineData(WorkoutPlanningState.Invalidated, WorkoutPlanningEvent.PreviewLoaded, WorkoutPlanningState.PreviewReady)]
    [InlineData(WorkoutPlanningState.Invalidated, WorkoutPlanningEvent.ClosePreview, WorkoutPlanningState.Initial)]
    public void StateTransitions_EvaluateAccordingToExpectedLifecycle(
        WorkoutPlanningState current,
        WorkoutPlanningEvent trigger,
        WorkoutPlanningState expected)
    {
        var result = WorkoutPlanningFlowRule.Transition(current, trigger);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(WorkoutPlanningState.PreviewReady, true, true, true)]
    [InlineData(WorkoutPlanningState.PreviewReady, false, true, false)]
    [InlineData(WorkoutPlanningState.PreviewReady, true, false, false)]
    [InlineData(WorkoutPlanningState.UncertainAcceptance, true, true, true)]
    [InlineData(WorkoutPlanningState.Accepting, true, true, false)]
    [InlineData(WorkoutPlanningState.Accepted, true, true, false)]
    [InlineData(WorkoutPlanningState.Invalidated, true, true, false)]
    [InlineData(WorkoutPlanningState.Initial, true, true, false)]
    public void CanAcceptInState_OnlyPermitsPreviewReadyAndUncertainRetryWithLeaseAndFeasibility(
        WorkoutPlanningState state,
        bool hasLease,
        bool feasible,
        bool expectedCanAccept)
    {
        var canAccept = WorkoutPlanningFlowRule.CanAcceptInState(state, hasLease, feasible);
        Assert.Equal(expectedCanAccept, canAccept);
    }

    [Theory]
    [InlineData(WorkoutPlanningState.PreviewReady, true, true)]
    [InlineData(WorkoutPlanningState.PreviewReady, false, false)]
    [InlineData(WorkoutPlanningState.UncertainAcceptance, true, false)]
    [InlineData(WorkoutPlanningState.Accepting, true, false)]
    public void RequiresReplacementConsentPrompt_OnlyAppliesToFirstAttemptOnReplacementPlan(
        WorkoutPlanningState state,
        bool replacesExistingPlan,
        bool expectedPromptRequired)
    {
        var prompt = WorkoutPlanningFlowRule.RequiresReplacementConsentPrompt(state, replacesExistingPlan);
        Assert.Equal(expectedPromptRequired, prompt);
    }
}
