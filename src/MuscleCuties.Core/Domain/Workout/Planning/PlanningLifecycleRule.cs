namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>The lease that makes one device the planner, with the fence that invalidates older writers.</summary>
public sealed record PlanningLease(Guid LeaseId, long Fence, DateTimeOffset ExpiresAtUtc);

/// <summary>Decides when a lease may be taken or renewed, so two devices cannot plan at once.</summary>
public static class PlanningLifecycleRule
{
    public static readonly TimeSpan FiveMinuteLeaseDuration = TimeSpan.FromMinutes(5);
    public const long InitialFence = 0;

    /// <summary>True when the lease is the one in force at the given moment.</summary>
    public static bool IsCurrentLease(PlanningLease? lease, Guid leaseId, long fence, DateTimeOffset now) =>
        lease is not null && lease.LeaseId == leaseId && lease.Fence == fence &&
        lease.ExpiresAtUtc > now;

    // Overflow must fail closed: an exhausted fence cannot safely be reused.
    /// <summary>The next fence value, or nothing when the sequence is exhausted.</summary>
    public static long? NextFence(long current) =>
        current < InitialFence || current == long.MaxValue ? null : current + 1;

    /// <summary>True when every required confirmation has been given.</summary>
    public static bool ConfirmationsSatisfied(
        bool replacementRequired,
        bool replacementConfirmed,
        bool reducedVolumeRequired,
        bool reducedVolumeAcknowledged) =>
        (!replacementRequired || replacementConfirmed) &&
        (!reducedVolumeRequired || reducedVolumeAcknowledged);
}
