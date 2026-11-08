using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// PG01: one foreground Generate/Regenerate intent activates an eligible plan. A replacement is only
/// activated when its consequence was disclosed on the initiating action, and an injury-excluded
/// No per-exercise acknowledgment gate exists; eligibility is decided by the reviewed rules.
/// </summary>
public sealed class WorkoutPlanningActivationRuleTests
{
    [Fact]
    public void RequestedFeasiblePlanActivatesWithoutASecondConfirmation()
    {
        Assert.True(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested: true, replacementDisclosed: false, replacesExistingPlan: false, feasible: true,
            persisted: true, hasLease: true, missingRequiredAnswers: false, styleChoiceRequired: false));
    }

    [Fact]
    public void ReplacementActivatesOnlyWhenItsConsequenceWasDisclosedUpFront()
    {
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, replacementDisclosed: false, replacesExistingPlan: true, true, true, true, false, false));
        Assert.True(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, replacementDisclosed: true, replacesExistingPlan: true, true, true, true, false, false));
    }

    [Theory]
    [InlineData(false)] // background refresh or navigation cannot authorize activation
    [InlineData(true)] // covered by the dedicated user-requested cases above
    public void BackgroundWorkNeverActivatesAPlan(bool userRequested)
    {
        var activated = WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested, true, false, true, true, true, false, false);
        Assert.Equal(userRequested, activated);
    }

    [Fact]
    public void IneligibleRequestsStayInTheDraft()
    {
        // Missing required answers, a strength-style choice, no lease, an unpersisted proposal, a
        // pending acknowledgment or an infeasible preview must not activate anything.
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, true, false, true, true, true, missingRequiredAnswers: true, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, true, false, true, true, true, false, styleChoiceRequired: true));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, true, false, true, true, hasLease: false, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, true, false, true, persisted: false, true, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, true, false, feasible: false, true, true, false, false));
    }

    [Fact]
    public void TheFirstPlanRuleStillAppliesToBackgroundGeneration()
    {
        // The source's tested first-plan exception is unchanged for a non-user-started load.
        Assert.True(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: false, feasible: true, persisted: true, missingRequiredAnswers: false,
            styleChoiceRequired: false, hasOmissions: false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: true, feasible: true, persisted: true, missingRequiredAnswers: false,
            styleChoiceRequired: false, hasOmissions: false));
    }

    [Theory]
    [InlineData(false, 4, 4, false)] // page left before completion
    [InlineData(true, 4, 3, false)] // a newer request superseded this one
    [InlineData(true, 4, 4, true)] // current appearance, current request
    public void OnlyTheCurrentAppearanceAndRequestMayAdoptAResult(
        bool appearanceIsCurrent,
        long requestVersion,
        long activeRequestVersion,
        bool expected)
    {
        Assert.Equal(expected, WorkoutPlanningFlowRule.IsCurrentGeneration(
            appearanceIsCurrent, requestVersion, activeRequestVersion));
    }
}
