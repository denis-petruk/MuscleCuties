using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.UseCases.Features.Account.Registration;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Registration;

public sealed class SignedRegistrationEmailReceiptVerifier : IRegistrationEmailReceiptVerifier
{
    private const int RegistrationPurposeValue = EmailReceiptLifetimePolicy.RegistrationPurpose;
    private const int MaximumTokenCharacters = 8192;
    private const int MinimumRsaKeyBits = 2048;
    private const long AllowedFutureIssueSeconds = 60;
    private readonly IReadOnlyDictionary<string, string> publicKeysById;
    private readonly string issuer;
    private readonly string audience;
    private readonly TimeProvider timeProvider;
    private readonly int expectedPurpose;

    public SignedRegistrationEmailReceiptVerifier(
        IReadOnlyDictionary<string, string> publicKeysById,
        string issuer,
        string audience,
        TimeProvider timeProvider,
        int expectedPurpose = RegistrationPurposeValue)
    {
        this.publicKeysById = publicKeysById;
        this.issuer = issuer;
        this.audience = audience;
        this.timeProvider = timeProvider;
        this.expectedPurpose = expectedPurpose;
    }

    public bool TryVerify(string token, string email, string challengeId, out RegistrationEmailReceipt? receipt)
    {
        receipt = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumTokenCharacters ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(challengeId) ||
            string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            return false;

        var segments = token.Split('.');
        if (segments.Length != 3)
            return false;

        try
        {
            using var headerDocument = JsonDocument.Parse(Decode(segments[0]));
            var header = headerDocument.RootElement;
            if (!HasExactFields(header, "alg", "kid", "typ") ||
                !IsString(header, "alg", "RS256") || !IsString(header, "typ", "JWT") ||
                !TryReadString(header, "kid", out var keyId) ||
                !publicKeysById.TryGetValue(keyId, out var publicKeyPem))
                return false;

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            if (rsa.KeySize < MinimumRsaKeyBits || !rsa.VerifyData(
                    Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}"),
                    Decode(segments[2]),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1))
                return false;

            using var claimsDocument = JsonDocument.Parse(Decode(segments[1]));
            var claims = claimsDocument.RootElement;
            if (!HasExactFields(claims, "iss", "aud", "email", "purpose", "challengeId", "iat", "exp", "jti") ||
                !IsString(claims, "iss", issuer) || !IsString(claims, "aud", audience) ||
                !IsString(claims, "email", AuthInputValidator.NormalizeEmail(email)) ||
                !IsString(claims, "challengeId", challengeId) ||
                !TryReadInt64(claims, "purpose", out var purpose) || purpose != expectedPurpose ||
                !TryReadInt64(claims, "iat", out var issuedAt) || !TryReadInt64(claims, "exp", out var expiresAt) ||
                !TryReadString(claims, "jti", out var receiptId))
                return false;

            var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
            if (issuedAt < 0 || issuedAt > now + AllowedFutureIssueSeconds || expiresAt <= now ||
                expiresAt <= issuedAt ||
                expiresAt - issuedAt > EmailReceiptLifetimePolicy.ForPurpose(expectedPurpose).TotalSeconds)
                return false;

            receipt = new RegistrationEmailReceipt(receiptId, DateTimeOffset.FromUnixTimeSeconds(expiresAt));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or CryptographicException or
                                           ArgumentException or OverflowException)
        {
            return false;
        }
    }

    private static bool HasExactFields(JsonElement element, params string[] expectedFields)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!expectedFields.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                return false;

        return seen.Count == expectedFields.Length;
    }

    private static bool IsString(JsonElement element, string name, string expected) =>
        TryReadString(element, name, out var actual) && string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool TryReadString(JsonElement element, string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadInt64(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out value);
    }

    private static byte[] Decode(string segment)
    {
        if (segment.Length == 0 || segment.Any(character =>
                character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
            throw new FormatException("The signed receipt contains an invalid base64url segment.");
        var padded = segment.Replace('-', '+').Replace('_', '/').PadRight((segment.Length + 3) / 4 * 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public sealed class Pbkdf2RegistrationPasswordHasher : IRegistrationPasswordHasher
{
    private const int WorkFactorIterations = 600_000;
    private const int SaltLengthBytes = 16;
    private const int DerivedKeyLengthBytes = 32;
    private const int MaximumEncodedHashCharacters = 256;
    private const string HashScheme = "PBKDF2-SHA256";
    // Server registration creates every accepted hash at this work factor. No legacy hashes are imported.
    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
    private static readonly byte[] DummyExpected = Rfc2898DeriveBytes.Pbkdf2(
        RandomNumberGenerator.GetBytes(DerivedKeyLengthBytes), DummySalt, WorkFactorIterations,
        HashAlgorithmName.SHA256, DerivedKeyLengthBytes);

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            WorkFactorIterations,
            HashAlgorithmName.SHA256,
            DerivedKeyLengthBytes);
        return string.Join(':', HashScheme, WorkFactorIterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(derivedKey));
    }

    public bool VerifyPassword(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (string.IsNullOrWhiteSpace(encodedHash) || encodedHash.Length > MaximumEncodedHashCharacters)
            return VerifyAgainstDummy(password);

        var parts = encodedHash.Split(':');
        if (parts.Length != 4 || !string.Equals(parts[0], HashScheme, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations != WorkFactorIterations)
            return VerifyAgainstDummy(password);

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltLengthBytes || expected.Length != DerivedKeyLengthBytes)
                return VerifyAgainstDummy(password);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return VerifyAgainstDummy(password);
        }
    }

    private static bool VerifyAgainstDummy(string password)
    {
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, DummySalt, WorkFactorIterations, HashAlgorithmName.SHA256, DerivedKeyLengthBytes);
        _ = CryptographicOperations.FixedTimeEquals(actual, DummyExpected);
        return false;
    }
}
