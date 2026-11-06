namespace MuscleCuties.Core.Domain.Workout;

/// <summary>
/// Internal Beta 1.0.0 scope for the workout schedule. Rescheduling is deferred to Beta 1.1, where it
/// moves an eligible unstarted session to an empty date under optimistic concurrency while keeping
/// session identity, ordering and injury lineage. Until that work is accepted, both the client surface
/// and the deployed route stay closed; this type is the one place that decides, so enabling the feature
/// later is a single reviewed change rather than a hunt for scattered call sites.
/// </summary>
public static class BetaWorkoutScope
{
    /// <summary>Stable code returned when a schedule change is requested outside the current scope.</summary>
    public const string ReschedulingGateCode = "WorkoutRescheduleNotAvailableInBeta1";

    /// <summary>
    /// Beta 1.0.0 does not offer rescheduling. Flip this only alongside the reviewed Beta 1.1
    /// concurrency, ordering and lineage work.
    /// </summary>
    public static bool ReschedulingEnabled => false;

    /// <summary>
    /// Whether the schedule may offer a move action for this day. Presentation and command guards both
    /// read this, so a hidden control and a rejected command can never disagree.
    /// </summary>
    public static bool CanOfferReschedule(bool isCompleted, bool isRestDay) =>
        ReschedulingEnabled && !isCompleted && !isRestDay;

    /// <summary>
    /// Fails closed for any caller that reaches the schedule-change path directly.
    /// </summary>
    public static void EnsureRescheduleAllowed()
    {
        if (!ReschedulingEnabled)
            throw new InvalidOperationException(ReschedulingGateCode);
    }
}
