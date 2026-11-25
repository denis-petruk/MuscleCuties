using MuscleCuties.UseCases.Features.Caching;

namespace MuscleCuties.UI.Services.Profile;

/// <summary>Local cache of progress photos, scoped to the signed-in account.</summary>
public interface ILocalPhotoCacheService
{
    Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task StoreAsync(Guid accountId, Guid photoId, Stream source, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(Guid accountId, Guid photoId, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>The cache key's store, marked separately so it can be backed by secure storage.</summary>
public interface IProtectedPhotoCacheKeyStore : ISnapshotCacheKeyStore
{
}
