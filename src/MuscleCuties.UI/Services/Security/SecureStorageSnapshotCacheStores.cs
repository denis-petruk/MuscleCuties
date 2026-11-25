using System.Text.Json;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Caching;

namespace MuscleCuties.UI.Services.Security;

/// <summary>Keeps the snapshot cache's AES key in the keychain, so cached responses stay encrypted</summary>
/// at rest and the key is not in the database file.</summary>
public sealed class SecureStorageSnapshotCacheKeyStore : ISnapshotCacheKeyStore
{
    private const string KeyName = "mc_snapshot_cache_aes256_key_v1";

    /// <summary>The stored key, or null when the cache has not been initialised yet.</summary>
    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var encoded = await SecureStorage.Default.GetAsync(KeyName).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(encoded) ? null : Convert.FromBase64String(encoded);
    }

    /// <summary>Stores the cache key, replacing any earlier one.</summary>
    public async Task WriteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(KeyName, Convert.ToBase64String(key)).ConfigureAwait(false);
    }

    /// <summary>Forgets the key, which makes every snapshot written with it unreadable.</summary>
    public Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(KeyName);
        return Task.CompletedTask;
    }
}

/// <summary>Session tokens in the keychain under a per-environment key, so switching backend does</summary>
/// not reuse another environment's session. Legacy tokens are migrated once on read.</summary>
public sealed class SecureStorageBackendSessionTokenStore(
    string? sessionKeyName = null,
    bool allowLegacyMigration = false) : IBackendSessionTokenStore
{
    private const string LegacySessionKeyName = "mc_backend_session_tokens_v1";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _sessionKeyName = string.IsNullOrWhiteSpace(sessionKeyName) ? LegacySessionKeyName : sessionKeyName;
    private readonly bool _allowLegacyMigration = allowLegacyMigration;

    /// <summary>Reads the tokens, migrating a legacy unpartitioned entry on first use and deleting it</summary>
    /// so two environments cannot share one session.</summary>
    public async Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await SecureStorage.Default.GetAsync(_sessionKeyName).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw) && _allowLegacyMigration && !string.Equals(_sessionKeyName, LegacySessionKeyName, StringComparison.Ordinal))
        {
            raw = await SecureStorage.Default.GetAsync(LegacySessionKeyName).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                // Safely migrate to partitioned key and remove legacy token to prevent cross-environment contamination
                await SecureStorage.Default.SetAsync(_sessionKeyName, raw).ConfigureAwait(false);
                SecureStorage.Default.Remove(LegacySessionKeyName);
            }
        }
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : JsonSerializer.Deserialize<BackendSessionTokens>(raw, SerializerOptions);
    }

    /// <summary>Stores the token set for this environment, replacing the previous one.</summary>
    public async Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        cancellationToken.ThrowIfCancellationRequested();
        var serialized = JsonSerializer.Serialize(tokens, SerializerOptions);
        await SecureStorage.Default.SetAsync(_sessionKeyName, serialized).ConfigureAwait(false);
    }

    /// <summary>Clears this environment's tokens and any legacy entry left by an older build.</summary>
    public Task ClearTokensAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(_sessionKeyName);
        if (!string.Equals(_sessionKeyName, LegacySessionKeyName, StringComparison.Ordinal))
        {
            SecureStorage.Default.Remove(LegacySessionKeyName);
        }
        return Task.CompletedTask;
    }
}
