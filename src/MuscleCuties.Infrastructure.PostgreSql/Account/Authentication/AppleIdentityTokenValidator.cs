using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;

public sealed record AppleVerifiedIdentity(string AppleUserId, string? Email);

public enum AppleIdentityValidationOutcome
{
    Valid,
    MissingToken,
    MissingConfiguration,
    InvalidFormat,
    UnsupportedAlgorithm,
    UnknownKey,
    InvalidSignature,
    WrongIssuer,
    WrongAudience,
    MissingSubject,
    InvalidNonce,
    Expired,
    InvalidIssuedAt,
    LifetimeTooLong
}

/// <summary>
/// Verifies a Sign in with Apple identity token against Apple's published keys and the
/// server-issued nonce challenge. Kept separate from the database-backed authentication
/// use case so the token contract can be tested without Postgres.
/// </summary>
public sealed class AppleIdentityTokenValidator(
    string appleAudience,
    IReadOnlyDictionary<string, string> applePublicKeysById,
    TimeProvider timeProvider)
{
    private const int MaximumIdentityTokenCharacters = 16_384;
    private const int MinimumRsaKeyBits = 2_048;
    // Apple Sign in with Apple issues iOS identityToken JWTs with a 24-hour (86,400 s) exp-iat window.
    // Replay protection is independently bounded to 5 minutes by the single-use server nonce challenge.
    private const long MaximumIdentityLifetimeSeconds = 86_400;
    private const long AllowedIssueTimeFutureSeconds = 60;
    private const string AppleIssuer = "https://appleid.apple.com";

    public AppleIdentityValidationOutcome Validate(
        string? token,
        string? nonce,
        out AppleVerifiedIdentity? identity)
    {
        identity = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumIdentityTokenCharacters ||
            string.IsNullOrWhiteSpace(nonce))
            return AppleIdentityValidationOutcome.MissingToken;
        if (applePublicKeysById.Count == 0 || string.IsNullOrWhiteSpace(appleAudience))
            return AppleIdentityValidationOutcome.MissingConfiguration;

        var pieces = token.Split('.');
        if (pieces.Length != 3)
            return AppleIdentityValidationOutcome.InvalidFormat;

        try
        {
            using var headerDoc = JsonDocument.Parse(Decode(pieces[0]));
            var header = headerDoc.RootElement;
            if (!header.TryGetProperty("alg", out var alg) || alg.GetString() != "RS256")
                return AppleIdentityValidationOutcome.UnsupportedAlgorithm;
            if (!header.TryGetProperty("kid", out var kid) || kid.GetString() is not { } keyId ||
                !applePublicKeysById.TryGetValue(keyId, out var pem))
                return AppleIdentityValidationOutcome.UnknownKey;

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < MinimumRsaKeyBits || !rsa.VerifyData(
                    Encoding.ASCII.GetBytes($"{pieces[0]}.{pieces[1]}"), Decode(pieces[2]),
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return AppleIdentityValidationOutcome.InvalidSignature;

            using var claimsDoc = JsonDocument.Parse(Decode(pieces[1]));
            var claims = claimsDoc.RootElement;
            var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
            if (!ReadString(claims, "iss", out var tokenIssuer) || tokenIssuer != AppleIssuer)
                return AppleIdentityValidationOutcome.WrongIssuer;
            if (!ReadString(claims, "aud", out var tokenAudience) || tokenAudience != appleAudience)
                return AppleIdentityValidationOutcome.WrongAudience;
            if (!ReadString(claims, "sub", out var appleUserId))
                return AppleIdentityValidationOutcome.MissingSubject;
            if (!ReadString(claims, "nonce", out var tokenNonce) || tokenNonce != Hash(nonce))
                return AppleIdentityValidationOutcome.InvalidNonce;
            if (!claims.TryGetProperty("exp", out var exp) || !exp.TryGetInt64(out var expiresAt) ||
                expiresAt <= now)
                return AppleIdentityValidationOutcome.Expired;
            if (!claims.TryGetProperty("iat", out var iat) || !iat.TryGetInt64(out var issuedAt) ||
                issuedAt > now + AllowedIssueTimeFutureSeconds || expiresAt <= issuedAt)
                return AppleIdentityValidationOutcome.InvalidIssuedAt;
            if (expiresAt - issuedAt > MaximumIdentityLifetimeSeconds)
                return AppleIdentityValidationOutcome.LifetimeTooLong;

            var email = ReadString(claims, "email", out var tokenEmail) ? tokenEmail : null;
            identity = new AppleVerifiedIdentity(appleUserId, email);
            return AppleIdentityValidationOutcome.Valid;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or
                                             CryptographicException or ArgumentException)
        {
            return AppleIdentityValidationOutcome.InvalidFormat;
        }
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool ReadString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static byte[] Decode(string value) =>
        Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/')
            .PadRight((value.Length + 3) / 4 * 4, '='));
}
