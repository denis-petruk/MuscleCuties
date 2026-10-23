using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MuscleCuties.Core.Services.Auth;

public sealed record VerifiedEmailReceipt(string Id, DateTimeOffset ExpiresAt);

public interface IEmailReceiptVerifier
{
    bool TryVerify(string token, string email, EmailChallengePurpose purpose,
        string? challengeId, out VerifiedEmailReceipt? receipt);
}

// Trust only explicitly packaged public keys. A missing allowlist fails closed.
public sealed class SignedEmailReceiptVerifier : IEmailReceiptVerifier
{
    private readonly string _audience;
    private readonly TimeProvider _clock;
    private readonly string _issuer;
    private readonly IReadOnlyDictionary<string, string> _publicKeysById;

    public SignedEmailReceiptVerifier(IReadOnlyDictionary<string, string> publicKeysById,
        string issuer, string audience, TimeProvider? clock = null)
    {
        _publicKeysById = publicKeysById;
        _issuer = issuer;
        _audience = audience;
        _clock = clock ?? TimeProvider.System;
    }

    public bool TryVerify(string token, string email, EmailChallengePurpose purpose,
        string? challengeId, out VerifiedEmailReceipt? receipt)
    {
        receipt = null;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(_issuer) ||
            string.IsNullOrWhiteSpace(_audience) || token.Length > 8192)
            return false;

        var segments = token.Split('.');
        if (segments.Length != 3) return false;

        try
        {
            using var headerDoc = JsonDocument.Parse(Decode(segments[0]));
            if (!TryValidateHeader(headerDoc.RootElement, out var publicKeyPem))
                return false;

            if (!VerifySignature(segments[0], segments[1], segments[2], publicKeyPem))
                return false;

            using var payloadDoc = JsonDocument.Parse(Decode(segments[1]));
            return TryValidateClaims(payloadDoc.RootElement, email, purpose, challengeId, out receipt);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or OverflowException
                                       or ArgumentException)
        {
            return false;
        }
    }

    private bool TryValidateHeader(JsonElement head, [NotNullWhen(true)] out string? publicKeyPem)
    {
        publicKeyPem = null;
        if (!ReceiptJsonHelper.HasExactFields(head, "alg", "kid", "typ") ||
            !ReceiptJsonHelper.IsString(head, "alg", "RS256") ||
            !ReceiptJsonHelper.IsString(head, "typ", "JWT") ||
            !ReceiptJsonHelper.TryGetNonemptyString(head, "kid", out var keyId) ||
            !_publicKeysById.TryGetValue(keyId, out publicKeyPem) ||
            string.IsNullOrWhiteSpace(publicKeyPem))
            return false;

        return true;
    }

    private static bool VerifySignature(string headerSegment, string payloadSegment, string signatureSegment,
        string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        if (rsa.KeySize < 2048) return false;

        var signedBytes = Encoding.ASCII.GetBytes($"{headerSegment}.{payloadSegment}");
        return rsa.VerifyData(signedBytes, Decode(signatureSegment), HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private bool TryValidateClaims(
        JsonElement claims,
        string email,
        EmailChallengePurpose purpose,
        string? challengeId,
        out VerifiedEmailReceipt? receipt)
    {
        receipt = null;
        if (!ReceiptJsonHelper.HasExactFields(claims, "iss", "aud", "email", "purpose", "challengeId", "iat", "exp",
                "jti") ||
            !ReceiptJsonHelper.IsString(claims, "iss", _issuer) ||
            !ReceiptJsonHelper.IsString(claims, "aud", _audience) ||
            !ReceiptJsonHelper.IsString(claims, "email", AuthInputValidator.NormalizeEmail(email)) ||
            !ReceiptJsonHelper.TryGetNonemptyString(claims, "challengeId", out var signedChallengeId) ||
            (challengeId is not null && !string.Equals(signedChallengeId, challengeId, StringComparison.Ordinal)) ||
            !ReceiptJsonHelper.TryGetNonemptyString(claims, "jti", out var id) || id.Length > 128 ||
            !ReceiptJsonHelper.TryGetInt64(claims, "purpose", out var receiptPurpose) ||
            receiptPurpose != (long)purpose ||
            !ReceiptJsonHelper.TryGetInt64(claims, "iat", out var issuedSeconds) ||
            !ReceiptJsonHelper.TryGetInt64(claims, "exp", out var expirySeconds))
            return false;

        var nowSeconds = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (issuedSeconds < 0 || expirySeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds() ||
            issuedSeconds > nowSeconds + 60 || expirySeconds <= nowSeconds ||
            expirySeconds <= issuedSeconds || expirySeconds - issuedSeconds > 900)
            return false;

        receipt = new VerifiedEmailReceipt(id, DateTimeOffset.FromUnixTimeSeconds(expirySeconds));
        return true;
    }

    private static byte[] Decode(string value)
    {
        if (value.Length == 0 || value.Any(ch =>
                ch is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
            throw new FormatException("Invalid base64url segment.");
        var padded = value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=');
        return Convert.FromBase64String(padded);
    }
}
