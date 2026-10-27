using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public enum WorkoutPlanningBackActionDto { ClosePreview, ResumeAfterApply, Leave }
public enum WorkoutPlanningSuccessDestinationDto { Dashboard, Workout }

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
        bool missingRequiredAnswers, bool styleChoiceRequired, bool hasOmissions, bool reducedVolume) =>
        WorkoutPlanningFlowRule.CanAutomaticallyApplyFirstPlan(replacesExistingPlan, feasible, persisted,
            missingRequiredAnswers, styleChoiceRequired, hasOmissions, reducedVolume);
}
