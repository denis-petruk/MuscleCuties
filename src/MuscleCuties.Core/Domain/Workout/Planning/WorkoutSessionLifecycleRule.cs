namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum WorkoutSessionLifecycleState
{
    Planned,
    InProgress,
    Completed
}

public static class WorkoutSessionLifecycleRule
{
    public const string ReasonPlannedCanBeModified = "PlannedSessionEligibleForModification";
    public const string ReasonCompletedOrInProgressSessionCannotBeModified = "CompletedOrInProgressSessionCannotBeModified";

    public static PolicyEvaluationResult<bool> CanModifySession(WorkoutSessionLifecycleState state)
    {
        if (state == WorkoutSessionLifecycleState.Planned)
        {
            return PolicyEvaluationResult.Allowed(
                true,
                ReasonPlannedCanBeModified,
                "Planned sessions can be adjusted, replaced, or regenerated.",
                PolicyRequirementCategory.HardConstraint);
        }

        return PolicyEvaluationResult.Blocked<bool>(
            ReasonCompletedOrInProgressSessionCannotBeModified,
            $"Sessions in state '{state}' are authoritative records and must not be altered by plan regeneration or proposals.",
            PolicyRequirementCategory.HardConstraint);
    }
}
