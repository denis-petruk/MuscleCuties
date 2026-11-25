using System.Security.Cryptography;
using System.Text;

namespace MuscleCuties.Core.Services.Auth;

/// <summary>The password hashing, with the legacy format still verified so an old account can migrate on its next</summary>
/// sign-in.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    public const string SchemePrefix = "PBKDF2-SHA256";
    public const int DefaultIterations = 600_000;
    public const int MinIterations = 10_000;
    public const int MaxIterations = 1_000_000;
    public const int SaltSizeBytes = 16;
    public const int HashSizeBytes = 32;

    /// <summary>The hasher the app uses unless a test substitutes one.</summary>
    public static PasswordHasher Default { get; } = new();

    /// <summary>A stored hash for a new password, in the current format.</summary>
    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes);

        return $"{SchemePrefix}:{DefaultIterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>Verifies a password and says whether its stored hash is in the old format, which is when the caller should</summary>
    /// rehash it.</summary>
    public (bool IsValid, bool NeedsMigration) VerifyPassword(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
            return (false, false);

        var trimmedHash = storedHash.Trim();

        // 1. Check for PBKDF2 format: PBKDF2-SHA256:<iterations>:<salt>:<hash> (or lowercase pbkdf2)
        if (trimmedHash.StartsWith(SchemePrefix + ":", StringComparison.OrdinalIgnoreCase) ||
            trimmedHash.StartsWith("pbkdf2:", StringComparison.OrdinalIgnoreCase))
            return VerifyPbkdf2(password, trimmedHash);

        // 2. Check for legacy SHA-256 format: 44-character Base64 string decoding to 32 bytes
        if (IsLegacySha256(trimmedHash, out var legacyStoredBytes))
        {
            var candidateBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var matches = CryptographicOperations.FixedTimeEquals(candidateBytes, legacyStoredBytes);
            return (matches, matches); // If matches legacy, it needs migration
        }

        return (false, false);
    }

    /// <summary>Verifies against the current key-derivation format.</summary>
    private static (bool IsValid, bool NeedsMigration) VerifyPbkdf2(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 4)
            return (false, false);

        if (!int.TryParse(parts[1], out var iterations) || iterations < MinIterations || iterations > MaxIterations)
            return (false, false);

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return (false, false);
        }

        if (salt.Length != SaltSizeBytes || expectedHash.Length != HashSizeBytes)
            return (false, false);

        byte[] actualHash;
        try
        {
            actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSizeBytes);
        }
        catch (Exception)
        {
            return (false, false);
        }

        var matches = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        var needsMigration = matches && (iterations < DefaultIterations ||
                                         !storedHash.StartsWith(SchemePrefix, StringComparison.Ordinal));
        return (matches, needsMigration);
    }

    /// <summary>Whether a stored hash is in the legacy format, so only those accounts are migrated.</summary>
    private static bool IsLegacySha256(string storedHash, out byte[] decodedBytes)
    {
        decodedBytes = [];
        if (storedHash.Length != 44)
            return false;

        Span<byte> buffer = stackalloc byte[32];
        if (Convert.TryFromBase64String(storedHash, buffer, out var bytesWritten) && bytesWritten == 32)
        {
            decodedBytes = buffer.ToArray();
            return true;
        }

        return false;
    }
}
