using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.Deletion;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;

public sealed class AppleAccountTokenRevoker(
    PostgresDbContext dbContext,
    HttpClient httpClient,
    TimeProvider timeProvider,
    string clientId,
    string? teamId,
    string? keyId,
    string? privateKeyPem) : IAppleAccountTokenRevoker
{
    public const string StoredRefreshTokenPrefix = "apple-rt:";
    public const string StoredAuthCodePrefix = "apple-code:";
    private const string AppleIssuerAudience = "https://appleid.apple.com";
    private const string AppleTokenEndpoint = "https://appleid.apple.com/auth/token";
    private const string AppleRevokeEndpoint = "https://appleid.apple.com/auth/revoke";
    private const int ClientSecretLifetimeSeconds = 300;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(clientId) &&
        !string.IsNullOrWhiteSpace(teamId) &&
        !string.IsNullOrWhiteSpace(keyId) &&
        !string.IsNullOrWhiteSpace(privateKeyPem);

    public async Task<string?> ExchangeAuthorizationCodeForRefreshTokenAsync(
        string? authorizationCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode) || !IsConfigured)
            return null;

        var clientSecret = CreateClientSecretJwt();
        using var request = new HttpRequestMessage(HttpMethod.Post, AppleTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = authorizationCode.Trim(),
                ["grant_type"] = "authorization_code"
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("refresh_token", out var rt) && rt.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(rt.GetString()))
            return rt.GetString();

        if (doc.RootElement.TryGetProperty("access_token", out var at) && at.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(at.GetString()))
            return at.GetString();

        return null;
    }

    public async Task<AppleTokenRevocationResult> RevokeAccountTokenAsync(
        Guid userId,
        string? authorizationCode = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return new AppleTokenRevocationResult(false, false, "A valid user ID is required.");

        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        var isAppleAccount = !string.IsNullOrWhiteSpace(user?.AppleUserId) ||
                             string.Equals(user?.CredentialType, "Apple", StringComparison.OrdinalIgnoreCase) ||
                             !string.IsNullOrWhiteSpace(authorizationCode);
        if (!isAppleAccount)
            return new AppleTokenRevocationResult(true, false);

        string? tokenToRevoke = null;
        var tokenTypeHint = "refresh_token";
        var storedCredential = user?.PasswordHash;

        if (!string.IsNullOrWhiteSpace(storedCredential) &&
            storedCredential.StartsWith(StoredRefreshTokenPrefix, StringComparison.Ordinal))
        {
            tokenToRevoke = storedCredential[StoredRefreshTokenPrefix.Length..].Trim();
        }
        else
        {
            var codeToExchange = !string.IsNullOrWhiteSpace(authorizationCode)
                ? authorizationCode.Trim()
                : (!string.IsNullOrWhiteSpace(storedCredential) &&
                   storedCredential.StartsWith(StoredAuthCodePrefix, StringComparison.Ordinal)
                    ? storedCredential[StoredAuthCodePrefix.Length..].Trim()
                    : null);

            if (!string.IsNullOrWhiteSpace(codeToExchange))
            {
                if (!IsConfigured)
                    return new AppleTokenRevocationResult(false, true, "Apple Sign-In revocation credentials are not configured.");

                tokenToRevoke = await ExchangeAuthorizationCodeForRefreshTokenAsync(codeToExchange, cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(tokenToRevoke))
                    return new AppleTokenRevocationResult(false, true, "Apple authorization code exchange failed before token revocation.");
            }
        }

        if (string.IsNullOrWhiteSpace(tokenToRevoke))
        {
            return IsConfigured
                ? new AppleTokenRevocationResult(false, true, "An Apple refresh token or fresh authorization code is required to revoke Sign in with Apple.")
                : new AppleTokenRevocationResult(true, true);
        }

        if (!IsConfigured)
            return new AppleTokenRevocationResult(false, true, "Apple Sign-In revocation credentials are not configured.");

        var clientSecret = CreateClientSecretJwt();
        using var revokeRequest = new HttpRequestMessage(HttpMethod.Post, AppleRevokeEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["token"] = tokenToRevoke,
                ["token_type_hint"] = tokenTypeHint
            })
        };

        using var response = await httpClient.SendAsync(revokeRequest, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? new AppleTokenRevocationResult(true, true)
            : new AppleTokenRevocationResult(false, true, "Apple token revocation endpoint rejected the request.");
    }

    public string CreateClientSecretJwt()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Apple Sign-In client secret credentials are not configured.");

        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var headerJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "ES256",
            kid = keyId!.Trim(),
            typ = "JWT"
        });
        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = teamId!.Trim(),
            iat = now,
            exp = now + ClientSecretLifetimeSeconds,
            aud = AppleIssuerAudience,
            sub = clientId.Trim()
        });

        var unsignedToken = $"{Base64UrlEncode(headerJson)}.{Base64UrlEncode(payloadJson)}";
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem!.Replace("\\n", "\n", StringComparison.Ordinal));
        var signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(unsignedToken),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
