namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>Finds the accounts that have cleanup work due, so the dispatcher does not scan every account.</summary>
public interface IDuePhotoCleanupOwnerDiscovery
{
    Task<IReadOnlyList<Guid>> DiscoverDueOwnersAsync(
        DateTime nowUtc,
        Guid? afterOwnerId,
        int limit,
        CancellationToken cancellationToken = default);
}
