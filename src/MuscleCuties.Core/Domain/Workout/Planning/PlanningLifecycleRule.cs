namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Versions captured when a proposal is generated and compared atomically at acceptance.</summary>
public sealed record PlanningInputVersions(
    long Preferences,
    long ActivePlan,
    long Answers,
    long Injuries,
    long Experience,
    string Catalog,
    string Policy,
    long TimeZone);

public sealed record PlanningLease(Guid LeaseId, long Fence, DateTimeOffset ExpiresAtUtc);

public static class PlanningLifecycleRule
{
    public static readonly TimeSpan FiveMinuteLeaseDuration = TimeSpan.FromMinutes(5);
    public const long InitialFence = 0;

    public static bool IsCurrentLease(PlanningLease? lease, Guid leaseId, long fence, DateTimeOffset now) =>
        lease is not null && lease.LeaseId == leaseId && lease.Fence == fence &&
        lease.ExpiresAtUtc > now;

    // Overflow must fail closed: an exhausted fence cannot safely be reused.
    public static long? NextFence(long current) =>
        current < InitialFence || current == long.MaxValue ? null : current + 1;

    public static bool ConfirmationsSatisfied(
        bool replacementRequired,
        bool replacementConfirmed,
        bool reducedVolumeRequired,
        bool reducedVolumeAcknowledged) =>
        (!replacementRequired || replacementConfirmed) &&
        (!reducedVolumeRequired || reducedVolumeAcknowledged);
}
