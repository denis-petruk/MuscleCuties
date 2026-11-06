using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public enum WorkoutPlanningBackActionDto { ClosePreview, ResumeAfterApply, Leave }
public enum WorkoutPlanningSuccessDestinationDto { Dashboard, Workout }

public enum WorkoutPlanningStateDto
{
    Initial,
    PreviewReady,
    Accepting,
    UncertainAcceptance,
    Accepted,
    NavigationPending,
    Invalidated,
    Completed
}

public enum WorkoutPlanningEventDto
{
    PreviewLoaded,
    StartAccepting,
    AcceptSucceeded,
    AcceptTimedOutOrNetworkFailed,
    AcceptConflictOrStale,
    AcceptValidationFailed,
    NavigateStarted,
    NavigateCompleted,
    InvalidateInputs,
    ClosePreview
}

/// <summary>Presentation-facing adapter for the domain's planning-flow decisions.</summary>
public static class WorkoutPlanningFlow
{
    public static WorkoutPlanningBackActionDto Back(bool hasPreview, bool navigationPending) =>
        WorkoutPlanningFlowRule.Back(hasPreview, navigationPending) switch
        {
            WorkoutPlanningBackAction.ClosePreview => WorkoutPlanningBackActionDto.ClosePreview,
            WorkoutPlanningBackAction.ResumeAfterApply => WorkoutPlanningBackActionDto.ResumeAfterApply,
            _ => WorkoutPlanningBackActionDto.Leave
        };

    public static WorkoutPlanningSuccessDestinationDto Success(bool replacesExistingPlan) =>
        WorkoutPlanningFlowRule.Success(replacesExistingPlan) == WorkoutPlanningSuccessDestination.Dashboard
            ? WorkoutPlanningSuccessDestinationDto.Dashboard : WorkoutPlanningSuccessDestinationDto.Workout;

    public static bool CanAccept(bool busy, bool accepted, bool navigationPending, bool invalidated,
        bool persisted, bool hasLease, bool feasible, bool uncertainAcceptance) =>
        WorkoutPlanningFlowRule.CanAccept(busy, accepted, navigationPending, invalidated, persisted,
            hasLease, feasible, uncertainAcceptance);

    public static bool CanAutomaticallyApplyFirstPlan(bool replacesExistingPlan, bool feasible, bool persisted,
        bool missingRequiredAnswers, bool styleChoiceRequired, bool hasOmissions) =>
        WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(replacesExistingPlan, feasible, persisted,
            missingRequiredAnswers, styleChoiceRequired, hasOmissions);

    public static PlanAcceptanceAcknowledgments AcceptanceAcknowledgments(bool replacesExistingPlan,
        bool reducedVolumeRequired, IEnumerable<int> exerciseConditionIds) =>
        WorkoutPlanningFlowRule.AcceptanceAcknowledgments(replacesExistingPlan, reducedVolumeRequired,
            exerciseConditionIds);

    public static WorkoutPlanningStateDto Transition(WorkoutPlanningStateDto current, WorkoutPlanningEventDto trigger) =>
        (WorkoutPlanningStateDto)WorkoutPlanningFlowRule.Transition(
            (WorkoutPlanningState)current,
            (WorkoutPlanningEvent)trigger);

    public static bool CanAcceptInState(WorkoutPlanningStateDto state, bool hasLease, bool feasible) =>
        WorkoutPlanningFlowRule.CanAcceptInState((WorkoutPlanningState)state, hasLease, feasible);

    public static bool RequiresReplacementConsentPrompt(WorkoutPlanningStateDto state, bool replacesExistingPlan) =>
        WorkoutPlanningFlowRule.RequiresReplacementConsentPrompt((WorkoutPlanningState)state, replacesExistingPlan);

    public static bool CanAutoActivateRequestedPlan(bool userRequested, bool replacementDisclosed,
        bool replacesExistingPlan, bool feasible, bool persisted, bool hasLease, bool missingRequiredAnswers,
        bool styleChoiceRequired, bool acknowledgmentUnavailable) =>
        WorkoutPlanningFlowRule.CanAutoActivateRequestedPlan(userRequested, replacementDisclosed,
            replacesExistingPlan, feasible, persisted, hasLease, missingRequiredAnswers,
            styleChoiceRequired, acknowledgmentUnavailable);

    public static bool RequiresInjuryAcknowledgment(int exerciseReviewConditionCount,
        int acknowledgedConditionCount) =>
        WorkoutPlanningFlowRule.RequiresInjuryAcknowledgment(exerciseReviewConditionCount,
            acknowledgedConditionCount);

    public static bool AdaptationChanged(IEnumerable<int> acknowledgedConditionIds,
        IEnumerable<int> returnedConditionIds) =>
        WorkoutPlanningFlowRule.AdaptationChanged(acknowledgedConditionIds, returnedConditionIds);

    public static bool IsCurrentGeneration(bool appearanceIsCurrent, long requestVersion,
        long activeRequestVersion) =>
        WorkoutPlanningFlowRule.IsCurrentGeneration(appearanceIsCurrent, requestVersion, activeRequestVersion);
}
