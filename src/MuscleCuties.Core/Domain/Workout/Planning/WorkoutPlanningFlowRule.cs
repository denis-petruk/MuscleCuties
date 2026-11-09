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
    bool ReducedVolumeAcknowledged);

public static class WorkoutPlanningFlowRule
{
    public static WorkoutPlanningBackAction Back(bool hasPreview, bool navigationPending) => navigationPending
        ? WorkoutPlanningBackAction.ResumeAfterApply
        : hasPreview ? WorkoutPlanningBackAction.ClosePreview : WorkoutPlanningBackAction.Leave;

    public static WorkoutPlanningSuccessDestination Success(bool replacesExistingPlan) => replacesExistingPlan
        ? WorkoutPlanningSuccessDestination.Workout : WorkoutPlanningSuccessDestination.Dashboard;

    /// <summary>
    /// The source's tested first-plan exception: a generated first plan that is feasible, persisted,
    /// complete and needs no further decision is applied without an Apply tap. A plan that fits only
    /// with reduced volume needs the user's acceptance, which Apply carries, so it is never applied
    /// automatically (G-02).
    /// </summary>
    public static bool CanAutomaticallyApplyFirstPlan(bool replacesExistingPlan, bool feasible, bool persisted,
        bool missingRequiredAnswers, bool styleChoiceRequired, bool hasOmissions, bool reducedVolume) =>
        !replacesExistingPlan && feasible && persisted && !missingRequiredAnswers && !styleChoiceRequired &&
        !hasOmissions && !reducedVolume;

    /// <summary>
    /// A foreground Generate/Regenerate may only activate the result without an Apply tap under the
    /// first-plan exception. An existing active plan is never replaced without explicit acceptance:
    /// the initiating control discloses the replacement consequence up front and the replacement
    /// stays in the preview until the user taps Apply plan (G-01). A reduced-volume plan likewise
    /// waits for Apply, which carries that acknowledgment (G-02).
    /// </summary>
    public static bool CanAutoActivateRequestedPlan(bool userRequested, bool replacesExistingPlan,
        bool feasible, bool persisted, bool hasLease, bool missingRequiredAnswers, bool styleChoiceRequired,
        bool reducedVolume) =>
        userRequested && !replacesExistingPlan && feasible && persisted && hasLease &&
        !missingRequiredAnswers && !styleChoiceRequired && !reducedVolume;

    /// <summary>
    /// Apply is the user's acceptance of a replacement. A second confirmation is only needed when the
    /// replacement consequence was not disclosed on an initiating Generate/Regenerate action.
    /// </summary>
    public static bool RequiresReplacementConfirmation(bool replacesExistingPlan,
        bool replacementDisclosedUpFront) =>
        replacesExistingPlan && !replacementDisclosedUpFront;

    /// <summary>
    /// CR01/PG01: an in-flight generation may only publish its result, start a lease or accept a plan
    /// while it is still the current appearance's current request.
    /// </summary>
    public static bool IsCurrentGeneration(bool appearanceIsCurrent, long requestVersion,
        long activeRequestVersion) =>
        appearanceIsCurrent && requestVersion == activeRequestVersion;

    public static PlanAcceptanceAcknowledgments AcceptanceAcknowledgments(bool replacesExistingPlan,
        bool reducedVolumeRequired) =>
        new(replacesExistingPlan, reducedVolumeRequired);

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
