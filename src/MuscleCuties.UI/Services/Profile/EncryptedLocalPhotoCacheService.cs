using System.Security.Cryptography;
using System.Text;

using MuscleCuties.Infrastructure.Caching;

namespace MuscleCuties.UI.Services.Profile;

/// <summary>Encrypted on-device photo cache. Files are bound to an account and a photo id, so a cached file</summary>
/// cannot be read under another account even when the path is known.</summary>
public sealed class EncryptedLocalPhotoCacheService(
    string appDataDirectory,
    IProtectedPhotoCacheKeyStore keyStore,
    Action<string> protectPath) : ILocalPhotoCacheService, IDisposable
{
    private const int AesKeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int AuthenticationTagSizeBytes = 16;
    private const int MaximumPhotoBytes = 20 * 1024 * 1024;
    private const int CopyBufferSizeBytes = 80 * 1024;
    private const string CacheDirectoryName = "photo-cache";
    private const string PhotoExtension = ".mcphoto";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Guid? _activeAccountId;

    /// <summary>Points the cache at an account. Switching accounts drops the previous account's key so its files</summary>
    /// stay unreadable.</summary>
    public async Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account identifier is required.", nameof(accountId));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeAccountId == accountId) return;
            _activeAccountId = null;
            await ClearCoreAsync(cancellationToken).ConfigureAwait(false);
            _activeAccountId = accountId;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stores the photo bytes encrypted.</summary>
    public async Task StoreAsync(Guid accountId, Guid photoId, Stream source,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(accountId, photoId);
        ArgumentNullException.ThrowIfNull(source);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureActiveAccount(accountId);
            var key = await GetOrCreateKeyAsync(cancellationToken).ConfigureAwait(false);
            using var input = new MemoryStream();
            var buffer = new byte[CopyBufferSizeBytes];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (input.Length + read > MaximumPhotoBytes)
                    throw new InvalidDataException("Photo exceeds the local cache size limit.");
                await input.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var ciphertext = new byte[(int)input.Length];
            var tag = new byte[AuthenticationTagSizeBytes];
            using (var aes = new AesGcm(key, AuthenticationTagSizeBytes))
                aes.Encrypt(nonce, input.GetBuffer().AsSpan(0, (int)input.Length), ciphertext, tag,
                    AssociatedData(accountId, photoId));
            var payload = new byte[NonceSizeBytes + AuthenticationTagSizeBytes + ciphertext.Length];
            nonce.CopyTo(payload, 0);
            tag.CopyTo(payload, NonceSizeBytes);
            ciphertext.CopyTo(payload, NonceSizeBytes + AuthenticationTagSizeBytes);

            var directory = Path.Combine(appDataDirectory, CacheDirectoryName);
            Directory.CreateDirectory(directory);
            protectPath(directory);
            var target = PhotoPath(directory, accountId, photoId);
            var temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, payload, cancellationToken).ConfigureAwait(false);
                protectPath(temporary);
                File.Move(temporary, target, overwrite: true);
                protectPath(target);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Opens the decrypted photo, when it is cached.</summary>
    public async Task<Stream?> OpenReadAsync(Guid accountId, Guid photoId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(accountId, photoId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureActiveAccount(accountId);
            var path = PhotoPath(Path.Combine(appDataDirectory, CacheDirectoryName), accountId, photoId);
            if (!File.Exists(path)) return null;
            var key = await keyStore.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (key is not { Length: AesKeySizeBytes }) return null;
            var payload = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (payload.Length < NonceSizeBytes + AuthenticationTagSizeBytes ||
                payload.Length > MaximumPhotoBytes + NonceSizeBytes + AuthenticationTagSizeBytes)
                return null;
            var plaintext = new byte[payload.Length - NonceSizeBytes - AuthenticationTagSizeBytes];
            try
            {
                using var aes = new AesGcm(key, AuthenticationTagSizeBytes);
                aes.Decrypt(payload.AsSpan(0, NonceSizeBytes),
                    payload.AsSpan(NonceSizeBytes + AuthenticationTagSizeBytes),
                    payload.AsSpan(NonceSizeBytes, AuthenticationTagSizeBytes), plaintext,
                    AssociatedData(accountId, photoId));
                return new MemoryStream(plaintext, writable: false);
            }
            catch (CryptographicException)
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

    /// <summary>Removes the cached photos.</summary>
    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        AccountScopedCacheClear.RunAsync(_gate, () => _activeAccountId = null, ClearCoreAsync, cancellationToken);

    /// <summary>The cache key from secure storage, created on first use; a key of the wrong length is treated as</summary>
    /// corruption and reported rather than used.</summary>
    private async Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken)
    {
        var key = await keyStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (key is { Length: AesKeySizeBytes }) return key;
        if (key is not null) throw new CryptographicException("Invalid photo cache key length.");
        key = RandomNumberGenerator.GetBytes(AesKeySizeBytes);
        await keyStore.WriteAsync(key, cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <summary>Deletes the cached files and the key together, reporting the first failure only after attempting</summary>
    /// both, so one failure cannot leave a key without files or files without a key.</summary>
    private async Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(appDataDirectory, CacheDirectoryName);
        Exception? directoryError = null;
        Exception? keyError = null;
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception)
        {
            directoryError = exception;
        }

        try
        {
            await keyStore.RemoveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            keyError = exception;
        }

        if (directoryError is not null && keyError is not null)
            throw new AggregateException("Photo cache files and key could not be fully cleared.",
                directoryError, keyError);
        if (directoryError is not null) throw directoryError;
        if (keyError is not null) throw keyError;
    }

    /// <summary>Rejects a read or write for an account other than the active one.</summary>
    private void EnsureActiveAccount(Guid accountId)
    {
        if (_activeAccountId != accountId)
            throw new UnauthorizedAccessException("The photo cache account changed.");
    }

    /// <summary>The file path of a cached photo.</summary>
    private static string PhotoPath(string directory, Guid accountId, Guid photoId) =>
        Path.Combine(directory, $"{accountId:N}_{photoId:N}{PhotoExtension}");

    /// <summary>The extra data that binds the ciphertext to the photo.</summary>
    private static byte[] AssociatedData(Guid accountId, Guid photoId) =>
        Encoding.ASCII.GetBytes($"{accountId:N}:{photoId:N}");

    /// <summary>Rejects missing identifiers before they become a path.</summary>
    private static void ValidateIdentity(Guid accountId, Guid photoId)
    {
        if (accountId == Guid.Empty || photoId == Guid.Empty)
            throw new ArgumentException("Account and photo identifiers are required.");
    }

    /// <summary>Releases the cache's gate.</summary>
    public void Dispose() => _gate.Dispose();
}
