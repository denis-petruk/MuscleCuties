namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum WorkoutPlanningBackAction { ClosePreview, ResumeAfterApply, Leave }
public enum WorkoutPlanningSuccessDestination { Dashboard, Workout }

public enum WorkoutPlanningState
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

public enum WorkoutPlanningEvent
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

    public static WorkoutPlanningState Transition(WorkoutPlanningState current, WorkoutPlanningEvent trigger) =>
        (current, trigger) switch
        {
            (WorkoutPlanningState.Initial, WorkoutPlanningEvent.PreviewLoaded) => WorkoutPlanningState.PreviewReady,

            (WorkoutPlanningState.PreviewReady, WorkoutPlanningEvent.StartAccepting) => WorkoutPlanningState.Accepting,
            (WorkoutPlanningState.PreviewReady, WorkoutPlanningEvent.InvalidateInputs) => WorkoutPlanningState.Invalidated,
            (WorkoutPlanningState.PreviewReady, WorkoutPlanningEvent.ClosePreview) => WorkoutPlanningState.Initial,

            (WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptSucceeded) => WorkoutPlanningState.Accepted,
            (WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptTimedOutOrNetworkFailed) => WorkoutPlanningState.UncertainAcceptance,
            (WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptConflictOrStale) => WorkoutPlanningState.Invalidated,
            (WorkoutPlanningState.Accepting, WorkoutPlanningEvent.AcceptValidationFailed) => WorkoutPlanningState.PreviewReady,

            (WorkoutPlanningState.UncertainAcceptance, WorkoutPlanningEvent.StartAccepting) => WorkoutPlanningState.Accepting,
            (WorkoutPlanningState.UncertainAcceptance, WorkoutPlanningEvent.InvalidateInputs) => WorkoutPlanningState.Invalidated,
            (WorkoutPlanningState.UncertainAcceptance, WorkoutPlanningEvent.ClosePreview) => WorkoutPlanningState.Initial,

            (WorkoutPlanningState.Accepted, WorkoutPlanningEvent.NavigateStarted) => WorkoutPlanningState.NavigationPending,

            (WorkoutPlanningState.NavigationPending, WorkoutPlanningEvent.NavigateCompleted) => WorkoutPlanningState.Completed,

            (WorkoutPlanningState.Invalidated, WorkoutPlanningEvent.PreviewLoaded) => WorkoutPlanningState.PreviewReady,
            (WorkoutPlanningState.Invalidated, WorkoutPlanningEvent.ClosePreview) => WorkoutPlanningState.Initial,

            _ => current
        };

    public static bool CanAcceptInState(WorkoutPlanningState state, bool hasLease, bool feasible) =>
        (state is WorkoutPlanningState.PreviewReady or WorkoutPlanningState.UncertainAcceptance) && hasLease && feasible;

    public static bool RequiresReplacementConsentPrompt(WorkoutPlanningState state, bool replacesExistingPlan) =>
        replacesExistingPlan && state == WorkoutPlanningState.PreviewReady;
}
