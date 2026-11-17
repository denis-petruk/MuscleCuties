using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class FirstReducedVolumeActivationTests
{
    [Fact]
    public void Feasible_reduced_volume_first_plan_can_activate_for_foreground_and_requested_generation()
    {
        Assert.True(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: false, feasible: true, persisted: true, missingRequiredAnswers: false,
            styleChoiceRequired: false, hasOmissions: false, reducedVolume: true));
        Assert.True(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested: true, replacesExistingPlan: false, feasible: true, persisted: true, hasLease: true,
            missingRequiredAnswers: false, styleChoiceRequired: false, reducedVolume: true));
    }

    [Fact]
    public void Reduced_volume_does_not_bypass_first_plan_safety_and_replacement_gates()
    {
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: true, feasible: true, persisted: true, missingRequiredAnswers: false,
            styleChoiceRequired: false, hasOmissions: false, reducedVolume: true));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(
            replacesExistingPlan: false, feasible: false, persisted: true, missingRequiredAnswers: false,
            styleChoiceRequired: false, hasOmissions: false, reducedVolume: true));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested: true, replacesExistingPlan: true, feasible: true, persisted: true, hasLease: true,
            missingRequiredAnswers: false, styleChoiceRequired: false, reducedVolume: true));
        Assert.False(WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(
            userRequested: true, replacesExistingPlan: false, feasible: true, persisted: true, hasLease: false,
            missingRequiredAnswers: false, styleChoiceRequired: false, reducedVolume: true));
    }
    [Fact]
    public void Automatic_first_plan_does_not_claim_an_acknowledgment_that_was_not_given()
    {
        var first = WorkoutPlanningFlowRule.AcceptanceAcknowledgments(false, true);
        var replacement = WorkoutPlanningFlowRule.AcceptanceAcknowledgments(true, true);

        Assert.False(first.ReducedVolumeAcknowledged);
        Assert.True(replacement.ReducedVolumeAcknowledged);
        Assert.True(replacement.ReplacementConfirmed);
    }
    [Fact]
    public void Feasible_first_partial_plan_applies_but_unsafe_plan_does_not()
    {
        Assert.True(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, true, true, false, false, true, true));
        Assert.False(WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(false, false, true, false, false, true, true));
    }
}
