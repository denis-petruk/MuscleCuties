namespace MuscleCuties.UI.Services.Profile;

/// <summary>Activates photo cache access for the signed-in account, reading the id lazily so each call follows the</summary>
/// current session.</summary>
public sealed class PhotoCacheAccountLifecycle(
    Func<Task<int>> getCurrentUserIdAsync,
    ILocalPhotoCacheService photoCache) : IPhotoCacheAccountLifecycle
{
    /// <summary>Activates the cache for the signed-in account and refuses when nobody is signed in.</summary>
    public async Task ActivateCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = await getCurrentUserIdAsync().ConfigureAwait(false);
        if (userId <= 0)
            throw new InvalidOperationException("A signed-in account is required to activate photo cache access.");

        await photoCache.ActivateAccountAsync(ToCacheAccountId(userId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The cache's account id for a server user id: the id in hex, padded, so it is stable across launches</summary>
    /// without storing a mapping.</summary>
    public static Guid ToCacheAccountId(int serverUserId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serverUserId);
        return Guid.ParseExact($"{serverUserId:x8}000000000000000000000000", "N");
    }
}
