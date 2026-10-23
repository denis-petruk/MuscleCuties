using System.Text.Json;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Caching;

namespace MuscleCuties.UI.Services.Security;

public sealed class SecureStorageSnapshotCacheKeyStore : ISnapshotCacheKeyStore
{
    private const string KeyName = "mc_snapshot_cache_aes256_key_v1";

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var encoded = await SecureStorage.Default.GetAsync(KeyName).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(encoded) ? null : Convert.FromBase64String(encoded);
    }

    public async Task WriteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(KeyName, Convert.ToBase64String(key)).ConfigureAwait(false);
    }

    public Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(KeyName);
        return Task.CompletedTask;
    }
}

public sealed class SecureStorageBackendSessionTokenStore : IBackendSessionTokenStore
{
    private const string SessionKeyName = "mc_backend_session_tokens_v1";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await SecureStorage.Default.GetAsync(SessionKeyName).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : JsonSerializer.Deserialize<BackendSessionTokens>(raw, SerializerOptions);
    }

    public async Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        cancellationToken.ThrowIfCancellationRequested();
        var serialized = JsonSerializer.Serialize(tokens, SerializerOptions);
        await SecureStorage.Default.SetAsync(SessionKeyName, serialized).ConfigureAwait(false);
    }

    public Task ClearTokensAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(SessionKeyName);
        return Task.CompletedTask;
    }
}
