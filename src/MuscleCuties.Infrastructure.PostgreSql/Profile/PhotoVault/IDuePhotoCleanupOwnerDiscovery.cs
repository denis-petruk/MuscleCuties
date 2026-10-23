namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public interface IDuePhotoCleanupOwnerDiscovery
{
    Task<IReadOnlyList<Guid>> DiscoverDueOwnersAsync(
        DateTime nowUtc,
        Guid? afterOwnerId,
        int limit,
        CancellationToken cancellationToken = default);
}
