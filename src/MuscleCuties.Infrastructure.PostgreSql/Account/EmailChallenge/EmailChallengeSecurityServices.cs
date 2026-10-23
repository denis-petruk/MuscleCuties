using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.UseCases.Features.Account.EmailChallenge;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;

public sealed class FirstPartyEmailChallengeDelivery(
    HttpClient httpClient,
    string? relayEndpoint,
    string? relayBearerToken) : IEmailChallengeDelivery
{
    public async Task<bool> SendAsync(string normalizedEmail, string code, int purpose, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(relayEndpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(relayBearerToken))
            return false;

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                recipient = normalizedEmail,
                verificationCode = code,
                purpose
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", relayBearerToken);
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}

public sealed class SignedEmailReceiptTokenIssuer(
    string keyId,
    string privateKeyPem,
    string issuer,
    string audience,
    TimeProvider timeProvider) : IEmailReceiptTokenIssuer
{
    private const int RegistrationPurpose = 1;
    private const int PasswordRecoveryPurpose = 2;
    private const int MinimumRsaKeyBits = 2_048;
    private const long AllowedIssueTimeFutureSeconds = 60;

    public string Issue(string normalizedEmail, int purpose, string challengeId, string receiptId, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
            !AuthInputValidation(normalizedEmail, purpose, challengeId, receiptId))
            throw new InvalidOperationException("Email receipt signing configuration or claims are invalid.");

        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var expires = expiresAtUtc.ToUnixTimeSeconds();
        if (expires <= now ||
            expires - now > EmailReceiptLifetimePolicy.ForPurpose(purpose).TotalSeconds)
            throw new InvalidOperationException("Email receipt lifetime is invalid.");

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        if (rsa.KeySize < MinimumRsaKeyBits)
            throw new InvalidOperationException("Email receipt signing key must contain at least 2048 bits.");
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = keyId, typ = "JWT" }));
        var claims = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuer, aud = audience, email = normalizedEmail, purpose, challengeId,
            iat = now, exp = expires, jti = receiptId
        }));
        var unsignedToken = $"{header}.{claims}";
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsignedToken), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsignedToken}.{Encode(signature)}";
    }

    private static bool AuthInputValidation(string email, int purpose, string challengeId, string receiptId) =>
        !string.IsNullOrWhiteSpace(email) && purpose is RegistrationPurpose or PasswordRecoveryPurpose && Guid.TryParse(challengeId, out _) &&
        !string.IsNullOrWhiteSpace(receiptId);

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
