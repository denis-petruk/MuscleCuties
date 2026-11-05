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

    /// <summary>
    /// PG01: a foreground Generate or Regenerate action is one intent to generate and activate. The
    /// consequence of replacing the active plan is disclosed on that initiating action, so an
    /// eligible replacement activates without a second confirmation. Everything that cannot be
    /// activated safely stays in the draft and returns the person to editable details instead.
    /// </summary>
    public static bool CanAutoActivateRequestedPlan(bool userRequested, bool replacementDisclosed,
        bool replacesExistingPlan, bool feasible, bool persisted, bool hasLease, bool missingRequiredAnswers,
        bool styleChoiceRequired, bool acknowledgmentUnavailable) =>
        userRequested && feasible && persisted && hasLease && !missingRequiredAnswers &&
        !styleChoiceRequired && !acknowledgmentUnavailable && (!replacesExistingPlan || replacementDisclosed);

    /// <summary>
    /// PG01: an injury-excluded exercise is the only permitted workout adaptation and the only extra
    /// decision. Any other required acknowledgment is an unresolved contract outcome: it may not be
    /// accepted on the person's behalf.
    /// </summary>
    public static bool RequiresInjuryAcknowledgment(int exerciseReviewConditionCount,
        int acknowledgedConditionCount) =>
        exerciseReviewConditionCount > 0 && acknowledgedConditionCount == 0;

    /// <summary>
    /// A revalidated adaptation that no longer matches the acknowledged exercise set must be
    /// disclosed again rather than accepted.
    /// </summary>
    public static bool AdaptationChanged(IEnumerable<int> acknowledgedConditionIds,
        IEnumerable<int> returnedConditionIds)
    {
        var acknowledged = acknowledgedConditionIds.Where(id => id > 0).ToHashSet();
        var returned = returnedConditionIds.Where(id => id > 0).ToHashSet();
        return !acknowledged.SetEquals(returned);
    }

    /// <summary>
    /// CR01/PG01: an in-flight generation may only publish its result, start a lease or accept a plan
    /// while it is still the current appearance's current request.
    /// </summary>
    public static bool IsCurrentGeneration(bool appearanceIsCurrent, long requestVersion,
        long activeRequestVersion) =>
        appearanceIsCurrent && requestVersion == activeRequestVersion;

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
