namespace MuscleCuties.UI.Services.Profile;

public sealed class PhotoCacheAccountLifecycle(
    Func<Task<int>> getCurrentUserIdAsync,
    ILocalPhotoCacheService photoCache) : IPhotoCacheAccountLifecycle
{
    public async Task ActivateCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = await getCurrentUserIdAsync().ConfigureAwait(false);
        if (userId <= 0)
            throw new InvalidOperationException("A signed-in account is required to activate photo cache access.");

        await photoCache.ActivateAccountAsync(ToCacheAccountId(userId), cancellationToken).ConfigureAwait(false);
    }

    public static Guid ToCacheAccountId(int serverUserId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serverUserId);
        return Guid.ParseExact($"{serverUserId:x8}000000000000000000000000", "N");
    }
}
