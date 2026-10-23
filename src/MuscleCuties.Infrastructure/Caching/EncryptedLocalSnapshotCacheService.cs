using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MuscleCuties.UseCases.Features.Caching;

namespace MuscleCuties.Infrastructure.Caching;

public sealed class EncryptedLocalSnapshotCacheService(
    string appDataDirectory,
    ISnapshotCacheKeyStore keyStore,
    Action<string>? protectPath = null,
    TimeSpan? defaultStaleWindow = null) : ILocalCacheService, IDisposable
{
    private const int AesKeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int AuthenticationTagSizeBytes = 16;
    private const int MaximumSnapshotBytes = 2 * 1024 * 1024;
    private const int DefaultStaleHours = 12;
    private const string CacheDirectoryName = "snapshot-cache";
    private const string SnapshotFileExtension = ".mcsnap";
    private const string ActiveWorkoutKind = "active-workout";
    private const string CycleStateKind = "cycle-state";
    private const string NutritionTargetsKind = "nutrition-targets";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _staleWindow = defaultStaleWindow ?? TimeSpan.FromHours(DefaultStaleHours);
    private Guid? _activeAccountId;

    public async Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier is required.", nameof(accountId));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeAccountId == accountId)
                return;

            // A new process has no active account in memory. Keep its encrypted snapshots
            // only when every persisted file belongs to the authenticated account.
            var directory = Path.Combine(appDataDirectory, CacheDirectoryName);
            var accountFilePrefix = $"{accountId:N}_";
            var hasOtherAccountFiles = Directory.Exists(directory) &&
                                       Directory.EnumerateFiles(directory).Any(path =>
                                           !Path.GetFileName(path).StartsWith(accountFilePrefix,
                                               StringComparison.Ordinal));
            if (_activeAccountId is not null || hasOtherAccountFiles)
                await ClearCoreAsync(cancellationToken).ConfigureAwait(false);
            _activeAccountId = accountId;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SaveActiveWorkoutSnapshotAsync(
        Guid accountId,
        ActiveWorkoutSessionSnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SaveSnapshotCoreAsync(
            accountId,
            ActiveWorkoutKind,
            snapshot,
            snapshot.ConcurrencyVersion,
            capturedAtUtc,
            cancellationToken);
    }

    public Task<CachedSnapshotEnvelope<ActiveWorkoutSessionSnapshot>?> LoadActiveWorkoutSnapshotAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        LoadSnapshotCoreAsync<ActiveWorkoutSessionSnapshot>(accountId, ActiveWorkoutKind, cancellationToken);

    public Task SaveCycleStateSnapshotAsync(
        Guid accountId,
        CycleStateSnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SaveSnapshotCoreAsync(
            accountId,
            CycleStateKind,
            snapshot,
            snapshot.ConcurrencyVersion,
            capturedAtUtc,
            cancellationToken);
    }

    public Task<CachedSnapshotEnvelope<CycleStateSnapshot>?> LoadCycleStateSnapshotAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        LoadSnapshotCoreAsync<CycleStateSnapshot>(accountId, CycleStateKind, cancellationToken);

    public Task SaveNutritionTargetsSnapshotAsync(
        Guid accountId,
        DailyNutritionTargetsSnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SaveSnapshotCoreAsync(
            accountId,
            NutritionTargetsKind,
            snapshot,
            snapshot.ConcurrencyVersion,
            capturedAtUtc,
            cancellationToken);
    }

    public Task<CachedSnapshotEnvelope<DailyNutritionTargetsSnapshot>?> LoadNutritionTargetsSnapshotAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        LoadSnapshotCoreAsync<DailyNutritionTargetsSnapshot>(accountId, NutritionTargetsKind, cancellationToken);

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _activeAccountId = null;
            await ClearCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveSnapshotCoreAsync<T>(
        Guid accountId,
        string snapshotKind,
        T snapshot,
        int sourceVersion,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier is required.", nameof(accountId));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureActiveAccount(accountId);
            var key = await GetOrCreateKeyAsync(cancellationToken).ConfigureAwait(false);
            var envelope = new CachedSnapshotEnvelope<T>(
                snapshot,
                capturedAtUtc,
                capturedAtUtc.Add(_staleWindow),
                sourceVersion);
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
            if (plaintext.Length > MaximumSnapshotBytes)
                throw new InvalidDataException("Snapshot exceeds the local encrypted cache size bound.");

            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[AuthenticationTagSizeBytes];
            using (var aes = new AesGcm(key, AuthenticationTagSizeBytes))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(accountId, snapshotKind));
            }

            var payload = new byte[NonceSizeBytes + AuthenticationTagSizeBytes + ciphertext.Length];
            nonce.CopyTo(payload, 0);
            tag.CopyTo(payload, NonceSizeBytes);
            ciphertext.CopyTo(payload, NonceSizeBytes + AuthenticationTagSizeBytes);

            var directory = Path.Combine(appDataDirectory, CacheDirectoryName);
            Directory.CreateDirectory(directory);
            protectPath?.Invoke(directory);
            var target = SnapshotPath(directory, accountId, snapshotKind);
            var temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, payload, cancellationToken).ConfigureAwait(false);
                protectPath?.Invoke(temporary);
                File.Move(temporary, target, overwrite: true);
                protectPath?.Invoke(target);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CachedSnapshotEnvelope<T>?> LoadSnapshotCoreAsync<T>(
        Guid accountId,
        string snapshotKind,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier is required.", nameof(accountId));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureActiveAccount(accountId);
            var path = SnapshotPath(Path.Combine(appDataDirectory, CacheDirectoryName), accountId, snapshotKind);
            if (!File.Exists(path))
                return null;

            var key = await keyStore.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (key is not { Length: AesKeySizeBytes })
                return null;

            var payload = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (payload.Length < NonceSizeBytes + AuthenticationTagSizeBytes ||
                payload.Length > MaximumSnapshotBytes + NonceSizeBytes + AuthenticationTagSizeBytes)
                return null;

            var plaintext = new byte[payload.Length - NonceSizeBytes - AuthenticationTagSizeBytes];
            try
            {
                using var aes = new AesGcm(key, AuthenticationTagSizeBytes);
                aes.Decrypt(
                    payload.AsSpan(0, NonceSizeBytes),
                    payload.AsSpan(NonceSizeBytes + AuthenticationTagSizeBytes),
                    payload.AsSpan(NonceSizeBytes, AuthenticationTagSizeBytes),
                    plaintext,
                    AssociatedData(accountId, snapshotKind));
                return JsonSerializer.Deserialize<CachedSnapshotEnvelope<T>>(plaintext, SerializerOptions);
            }
            catch (CryptographicException)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                return null;
            }
            catch (JsonException)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken)
    {
        var key = await keyStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (key is { Length: AesKeySizeBytes })
            return key;
        if (key is not null)
            throw new CryptographicException("Invalid snapshot cache key length.");

        key = RandomNumberGenerator.GetBytes(AesKeySizeBytes);
        await keyStore.WriteAsync(key, cancellationToken).ConfigureAwait(false);
        return key;
    }

    private async Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        await keyStore.RemoveAsync(cancellationToken).ConfigureAwait(false);
        var directory = Path.Combine(appDataDirectory, CacheDirectoryName);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private void EnsureActiveAccount(Guid accountId)
    {
        if (_activeAccountId != accountId)
            throw new UnauthorizedAccessException("The encrypted snapshot cache account changed.");
    }

    private static string SnapshotPath(string directory, Guid accountId, string snapshotKind) =>
        Path.Combine(directory, $"{accountId:N}_{snapshotKind}{SnapshotFileExtension}");

    private static byte[] AssociatedData(Guid accountId, string snapshotKind) =>
        Encoding.ASCII.GetBytes($"{accountId:N}:{snapshotKind}:v1");

    public void Dispose() => _gate.Dispose();
}

public sealed class VersionedCloudAuthorityGate(
    CloudAuthorityMode initialMode = CloudAuthorityMode.ServerAuthoritativeCutover,
    bool initialNetworkOnline = true) : ICloudAuthorityGate
{
    private const int CurrentAuthorityVersion = 1;
    private const string OfflineWriteRejectionCode = "offlineWriteNotSupported";
    private const string LocalSqliteWriteBlockedAfterCutoverCode = "localSqliteWriteBlockedAfterCutover";

    private int _mode = (int)initialMode;
    private int _isOnline = initialNetworkOnline ? 1 : 0;

    public int AuthorityVersion => CurrentAuthorityVersion;

    public CloudAuthorityMode CurrentMode => (CloudAuthorityMode)Volatile.Read(ref _mode);

    public bool IsNetworkOnline => Volatile.Read(ref _isOnline) == 1;

    public void SetAuthorityMode(CloudAuthorityMode mode) =>
        Volatile.Write(ref _mode, (int)mode);

    public void SetNetworkOnline(bool isOnline) =>
        Volatile.Write(ref _isOnline, isOnline ? 1 : 0);

    public OfflineWriteCheckResult EvaluateWriteAllowed(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (CurrentMode == CloudAuthorityMode.ServerAuthoritativeCutover && !IsNetworkOnline)
        {
            return new OfflineWriteCheckResult(
                Allowed: false,
                RejectionCode: OfflineWriteRejectionCode,
                UserFacingMessage: $"Cannot save '{operationName}' while offline. Cached workout, cycle, and nutrition snapshots remain available read-only.");
        }

        return new OfflineWriteCheckResult(Allowed: true, RejectionCode: null, UserFacingMessage: null);
    }

    public OfflineWriteCheckResult EvaluateLocalSqliteWriteAllowed(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (CurrentMode == CloudAuthorityMode.ServerAuthoritativeCutover)
        {
            return new OfflineWriteCheckResult(
                Allowed: false,
                RejectionCode: LocalSqliteWriteBlockedAfterCutoverCode,
                UserFacingMessage: $"Local SQLite write '{operationName}' is blocked under server-authoritative cutover v{CurrentAuthorityVersion}.");
        }

        return new OfflineWriteCheckResult(Allowed: true, RejectionCode: null, UserFacingMessage: null);
    }
}
