using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// G-01/G-02: activation without an Apply tap is limited to the source's tested first-plan exception.
/// An existing active plan is replaced only through Apply plan; the initiating Generate/Regenerate
/// discloses that consequence up front but never activates the replacement by itself. A plan that
/// fits only with reduced volume waits for Apply, which carries that acknowledgment.
/// </summary>
public sealed class WorkoutPlanningActivationRuleTests
{
    [Fact]
    public void RequestedFeasibleFirstPlanActivatesWithoutASecondTap()
    {
        Assert.True(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested: true, replacesExistingPlan: false, feasible: true, persisted: true,
            hasLease: true, missingRequiredAnswers: false, styleChoiceRequired: false, reducedVolume: false));
    }

    [Fact]
    public void ReplacementNeverActivatesWithoutApplyEvenWhenRequestedInTheForeground()
    {
        // The replacement consequence is disclosed on the initiating action, but that disclosure is not
        // acceptance: the replacement stays in the preview until the user taps Apply plan.
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested: true, replacesExistingPlan: true, feasible: true, persisted: true,
            hasLease: true, missingRequiredAnswers: false, styleChoiceRequired: false, reducedVolume: false));
    }

    [Fact]
    public void ReducedVolumeFirstPlanWaitsForApply()
    {
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested: true, replacesExistingPlan: false, feasible: true, persisted: true,
            hasLease: true, missingRequiredAnswers: false, styleChoiceRequired: false, reducedVolume: true));
    }

    [Theory]
    [InlineData(false)] // background refresh or navigation cannot authorize activation
    [InlineData(true)] // covered by the dedicated user-requested cases above
    public void BackgroundWorkNeverActivatesAPlan(bool userRequested)
    {
        var activated = WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested, false, true, true, true, false, false, false);
        Assert.Equal(userRequested, activated);
    }

    [Fact]
    public void IneligibleRequestsStayInTheDraft()
    {
        // Missing required answers, a strength-style choice, no lease, an unpersisted proposal or an
        // infeasible preview must not activate anything.
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, false, true, true, true, missingRequiredAnswers: true, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, false, true, true, true, false, styleChoiceRequired: true, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, false, true, true, hasLease: false, false, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, false, true, persisted: false, true, false, false, false));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            true, false, feasible: false, true, true, false, false, false));
    }

    [Fact]
    public void TheFirstPlanRuleStillAppliesToBackgroundGeneration()
    {
        // The source's tested first-plan exception is unchanged for a non-user-started load.
        Assert.True(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: false, feasible: true, persisted: true, missingRequiredAnswers: false,
            styleChoiceRequired: false, hasOmissions: false, reducedVolume: false));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: true, feasible: true, persisted: true, missingRequiredAnswers: false,
            styleChoiceRequired: false, hasOmissions: false, reducedVolume: false));
    }

    [Theory]
    [InlineData(false, false, false)] // first plan: nothing to replace
    [InlineData(false, true, false)]
    [InlineData(true, true, false)] // disclosed on Generate: the Apply tap is the acceptance
    [InlineData(true, false, true)] // never disclosed: Apply still states the replacement first
    public void ReplacementConfirmationIsOnlyAskedWhenTheConsequenceWasNotDisclosed(
        bool replacesExistingPlan, bool disclosedUpFront, bool expected)
    {
        Assert.Equal(expected, WorkoutPlanningFlowRule.RequiresReplacementConfirmation(
            replacesExistingPlan, disclosedUpFront));
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
