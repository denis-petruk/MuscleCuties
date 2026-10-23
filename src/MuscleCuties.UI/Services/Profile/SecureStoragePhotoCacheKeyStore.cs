namespace MuscleCuties.UI.Services.Profile;

public sealed class SecureStoragePhotoCacheKeyStore : IProtectedPhotoCacheKeyStore
{
    private const string StorageKey = "Key_PhotoCacheKey";

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
        if (value is null) return null;
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            SecureStorage.Default.Remove(StorageKey);
            return null;
        }
    }

    public async Task WriteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(StorageKey, Convert.ToBase64String(key)).ConfigureAwait(false);
    }

    public Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
