namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum WorkoutPlanningBackAction { ClosePreview, ResumeAfterApply, Leave }
public enum WorkoutPlanningSuccessDestination { Dashboard, Workout }

/// <summary>Everything the user accepts by pressing Apply on the visible preview.</summary>
public sealed record PlanAcceptanceAcknowledgments(
    bool ReplacementConfirmed,
    bool ReducedVolumeAcknowledged,
    IReadOnlySet<int> AcknowledgedExerciseConditionIds);

public static class WorkoutPlanningFlowRule
{
    public static WorkoutPlanningBackAction Back(bool hasPreview, bool navigationPending) => navigationPending
        ? WorkoutPlanningBackAction.ResumeAfterApply
        : hasPreview ? WorkoutPlanningBackAction.ClosePreview : WorkoutPlanningBackAction.Leave;

    public static WorkoutPlanningSuccessDestination Success(bool replacesExistingPlan) => replacesExistingPlan
        ? WorkoutPlanningSuccessDestination.Workout : WorkoutPlanningSuccessDestination.Dashboard;

    public static bool CanAutomaticallyApplyFirstPlan(bool replacesExistingPlan, bool feasible, bool persisted,
        bool missingRequiredAnswers, bool styleChoiceRequired, bool hasOmissions) =>
        !replacesExistingPlan && feasible && persisted && !missingRequiredAnswers && !styleChoiceRequired &&
        !hasOmissions;

    public static PlanAcceptanceAcknowledgments AcceptanceAcknowledgments(bool replacesExistingPlan,
        bool reducedVolumeRequired, IEnumerable<int> exerciseConditionIds) =>
        new(replacesExistingPlan, reducedVolumeRequired,
            exerciseConditionIds.Where(id => id > 0).ToHashSet());

    public static bool CanAccept(bool busy, bool accepted, bool navigationPending, bool invalidated,
        bool persisted, bool hasLease, bool feasible, bool uncertainAcceptance) =>
        !busy && !accepted && !navigationPending && persisted && hasLease && feasible &&
        (!invalidated || uncertainAcceptance);
}
