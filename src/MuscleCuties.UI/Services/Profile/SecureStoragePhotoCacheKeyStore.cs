namespace MuscleCuties.UI.Services.Profile;

/// <summary>Keeps the photo cache key in secure storage, base64-encoded because the store holds text.</summary>
public sealed class SecureStoragePhotoCacheKeyStore : IProtectedPhotoCacheKeyStore
{
    private const string StorageKey = "Key_PhotoCacheKey";

    /// <summary>The stored key, or null when there is none; an unreadable entry is removed so the cache can rebuild</summary>
    /// instead of failing forever.</summary>
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

    /// <summary>Stores the key, replacing any earlier one.</summary>
    public async Task WriteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(StorageKey, Convert.ToBase64String(key)).ConfigureAwait(false);
    }

    /// <summary>Forgets the key, which makes every file encrypted with it unreadable.</summary>
    public Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
