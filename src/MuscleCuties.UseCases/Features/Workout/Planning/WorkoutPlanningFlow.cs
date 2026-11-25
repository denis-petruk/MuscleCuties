using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Where the client should go when planning is abandoned.</summary>
public enum WorkoutPlanningBackActionDto { ClosePreview, ResumeAfterApply, Leave }
/// <summary>Where the client should go once a plan is accepted.</summary>
public enum WorkoutPlanningSuccessDestinationDto { Dashboard, Workout }

/// <summary>Presentation-facing adapter for the domain's planning-flow decisions.</summary>
public static class WorkoutPlanningFlow
{
    /// <summary>Where the back action goes from the current step.</summary>
    public static WorkoutPlanningBackActionDto Back(bool hasPreview, bool navigationPending) =>
        WorkoutPlanningFlowRule.Back(hasPreview, navigationPending) switch
        {
            WorkoutPlanningBackAction.ClosePreview => WorkoutPlanningBackActionDto.ClosePreview,
            WorkoutPlanningBackAction.ResumeAfterApply => WorkoutPlanningBackActionDto.ResumeAfterApply,
            _ => WorkoutPlanningBackActionDto.Leave
        };

    /// <summary>Where a successful plan lands.</summary>
    public static WorkoutPlanningSuccessDestinationDto Success(bool replacesExistingPlan) =>
        WorkoutPlanningFlowRule.Success(replacesExistingPlan) == WorkoutPlanningSuccessDestination.Dashboard
            ? WorkoutPlanningSuccessDestinationDto.Dashboard : WorkoutPlanningSuccessDestinationDto.Workout;

    /// <summary>True when the plan may be accepted.</summary>
    public static bool CanAccept(bool busy, bool accepted, bool navigationPending, bool invalidated,
        bool persisted, bool hasLease, bool feasible, bool uncertainAcceptance) =>
        WorkoutPlanningFlowRule.CanAccept(busy, accepted, navigationPending, invalidated, persisted,
            hasLease, feasible, uncertainAcceptance);

    /// <summary>True when the first plan may be applied without asking.</summary>
    public static bool CanAutomaticallyApplyFirstPlan(bool replacesExistingPlan, bool feasible, bool persisted,
        bool missingRequiredAnswers, bool styleChoiceRequired, bool hasOmissions, bool reducedVolume) =>
        WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(replacesExistingPlan, feasible, persisted,
            missingRequiredAnswers, styleChoiceRequired, hasOmissions, reducedVolume);

    /// <summary>The acknowledgements accepting the plan needs.</summary>
    public static PlanAcceptanceAcknowledgments AcceptanceAcknowledgments(bool replacesExistingPlan,
        bool reducedVolumeRequired) =>
        WorkoutPlanningFlowRule.AcceptanceAcknowledgments(replacesExistingPlan, reducedVolumeRequired);

    /// <summary>True when the requested plan may be activated automatically.</summary>
    public static bool CanAutoActivateRequestedPlan(bool userRequested, bool replacesExistingPlan,
        bool feasible, bool persisted, bool hasLease, bool missingRequiredAnswers, bool styleChoiceRequired,
        bool reducedVolume) =>
        WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(userRequested, replacesExistingPlan, feasible,
            persisted, hasLease, missingRequiredAnswers, styleChoiceRequired, reducedVolume);

    /// <summary>True when the user must confirm replacing the active plan.</summary>
    public static bool RequiresReplacementConfirmation(bool replacesExistingPlan,
        bool replacementDisclosedUpFront) =>
        WorkoutPlanningFlowRule.RequiresReplacementConfirmation(replacesExistingPlan,
            replacementDisclosedUpFront);

    /// <summary>True when the generation result still matches the current request.</summary>
    public static bool IsCurrentGeneration(bool appearanceIsCurrent, long requestVersion,
        long activeRequestVersion) =>
        WorkoutPlanningFlowRule.IsCurrentGeneration(appearanceIsCurrent, requestVersion, activeRequestVersion);
}
