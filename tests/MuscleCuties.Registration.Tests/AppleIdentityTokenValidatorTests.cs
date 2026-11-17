using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;
using Xunit;

namespace MuscleCuties.Registration.Tests;

public sealed class AppleIdentityTokenValidatorTests
{
    private const string Audience = "com.musclecuties.app";
    private const string KeyId = "syntheticAppleKey";
    private const string RawNonce = "synthetic-raw-nonce";

    [Fact]
    public void ValidTokenReturnsIdentityAndEmail()
    {
        using var keys = new SyntheticAppleKeys();
        var validator = keys.CreateValidator();
        var token = keys.CreateToken(rawNonce: RawNonce, email: "member@example.invalid");

        var outcome = validator.Validate(token, RawNonce, out var identity);

        Assert.Equal(AppleIdentityValidationOutcome.Valid, outcome);
        Assert.NotNull(identity);
        Assert.Equal("synthetic-apple-subject", identity!.AppleUserId);
        Assert.Equal("member@example.invalid", identity.Email);
    }

    [Fact]
    public void DifferentNonceIsRejected()
    {
        using var keys = new SyntheticAppleKeys();
        var validator = keys.CreateValidator();
        var token = keys.CreateToken(rawNonce: "another-nonce");

        var outcome = validator.Validate(token, RawNonce, out var identity);

        Assert.Equal(AppleIdentityValidationOutcome.InvalidNonce, outcome);
        Assert.Null(identity);
    }

    [Fact]
    public void DifferentAudienceIsRejected()
    {
        using var keys = new SyntheticAppleKeys();
        var validator = keys.CreateValidator();
        var token = keys.CreateToken(rawNonce: RawNonce, audience: "com.example.other");

        var outcome = validator.Validate(token, RawNonce, out _);

        Assert.Equal(AppleIdentityValidationOutcome.WrongAudience, outcome);
    }

    [Fact]
    public void DifferentIssuerIsRejected()
    {
        using var keys = new SyntheticAppleKeys();
        var validator = keys.CreateValidator();
        var token = keys.CreateToken(rawNonce: RawNonce, issuer: "https://example.invalid");

        var outcome = validator.Validate(token, RawNonce, out _);

        Assert.Equal(AppleIdentityValidationOutcome.WrongIssuer, outcome);
    }

    [Fact]
    public void UnknownKeyIdentifierIsRejected()
    {
        using var keys = new SyntheticAppleKeys();
        var validator = keys.CreateValidator();
        var token = keys.CreateToken(rawNonce: RawNonce, keyId: "notApple");

        var outcome = validator.Validate(token, RawNonce, out _);

        Assert.Equal(AppleIdentityValidationOutcome.UnknownKey, outcome);
    }

    [Fact]
    public void ExpiredTokenIsRejected()
    {
        using var keys = new SyntheticAppleKeys();
        var validator = keys.CreateValidator();
        var now = DateTimeOffset.UtcNow;
        var token = keys.CreateToken(rawNonce: RawNonce, issuedAt: now.AddHours(-2), expiresAt: now.AddHours(-1));

        var outcome = validator.Validate(token, RawNonce, out _);

        Assert.Equal(AppleIdentityValidationOutcome.Expired, outcome);
    }

    [Fact]
    public void TokenLifetimeBeyondOneHourIsRejected()
    {
        using var keys = new SyntheticAppleKeys();
        var validator = keys.CreateValidator();
        var now = DateTimeOffset.UtcNow;
        var token = keys.CreateToken(rawNonce: RawNonce, issuedAt: now, expiresAt: now.AddHours(2));

        var outcome = validator.Validate(token, RawNonce, out _);

        Assert.Equal(AppleIdentityValidationOutcome.LifetimeTooLong, outcome);
    }

    private sealed class SyntheticAppleKeys : IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public AppleIdentityTokenValidator CreateValidator() =>
            new(Audience,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [KeyId] = _rsa.ExportSubjectPublicKeyInfoPem()
                },
                TimeProvider.System);

        public string CreateToken(
            string rawNonce,
            string? email = null,
            string audience = Audience,
            string issuer = "https://appleid.apple.com",
            string keyId = KeyId,
            DateTimeOffset? issuedAt = null,
            DateTimeOffset? expiresAt = null)
        {
            var issued = issuedAt ?? DateTimeOffset.UtcNow;
            var expires = expiresAt ?? issued.AddMinutes(5);
            var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = keyId }));
            var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
            {
                ["iss"] = issuer,
                ["aud"] = audience,
                ["sub"] = "synthetic-apple-subject",
                ["nonce"] = AppleIdentityTokenValidator.Hash(rawNonce),
                ["iat"] = issued.ToUnixTimeSeconds(),
                ["exp"] = expires.ToUnixTimeSeconds(),
                ["email"] = email
            }));
            var signature = _rsa.SignData(
                Encoding.ASCII.GetBytes($"{header}.{payload}"),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            return $"{header}.{payload}.{Encode(signature)}";
        }

        public void Dispose() => _rsa.Dispose();

        private static string Encode(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
