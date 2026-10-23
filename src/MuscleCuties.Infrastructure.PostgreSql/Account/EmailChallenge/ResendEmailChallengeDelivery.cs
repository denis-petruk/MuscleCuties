using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MuscleCuties.UseCases.Features.Account.EmailChallenge;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;

public sealed class ResendEmailChallengeDelivery(
    HttpClient httpClient,
    string? apiKey,
    string? fromEmail = null,
    string? apiEndpoint = null) : IEmailChallengeDelivery
{
    private const string DefaultResendEndpoint = "https://api.resend.com/emails";
    private const string ResendApiHost = "api.resend.com";
    private const string DefaultFromAddress = "MuscleCuties <noreply@musclecuties.app>";
    private const int RegistrationPurpose = 1;
    private const int PasswordRecoveryPurpose = 2;
    private const int RequiredCodeLength = 6;

    public async Task<bool> SendAsync(
        string normalizedEmail,
        string code,
        int purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(normalizedEmail) ||
            string.IsNullOrWhiteSpace(code) ||
            code.Length != RequiredCodeLength)
        {
            return false;
        }

        var endpoint = apiEndpoint ?? DefaultResendEndpoint;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, ResendApiHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sender = !string.IsNullOrWhiteSpace(fromEmail) ? fromEmail : DefaultFromAddress;

        string subject;
        string textBody;
        string htmlBody;

        switch (purpose)
        {
            case RegistrationPurpose:
                subject = "MuscleCuties — Verify your email";
                textBody = BuildRegistrationText(code);
                htmlBody = BuildRegistrationHtml(code);
                break;

            case PasswordRecoveryPurpose:
                subject = "MuscleCuties — Password Reset";
                textBody = BuildPasswordResetText(code);
                htmlBody = BuildPasswordResetHtml(code);
                break;

            default:
                return false;
        }

        var payload = new ResendEmailPayload
        {
            From = sender,
            To = [normalizedEmail],
            Subject = subject,
            Html = htmlBody,
            Text = textBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        try
        {
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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

    private static string BuildRegistrationText(string code) =>
        $"""
        Welcome to MuscleCuties!

        Your 6-digit verification code is: {code}

        This code expires in 10 minutes. Enter it in the app to complete your registration.
        If you did not create a MuscleCuties account, you can safely ignore this email.
        """;

    private static string BuildRegistrationHtml(string code) =>
        $$"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>Confirm Your Email</title>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #0c0b10; color: #f4f3f7; padding: 40px 20px; margin: 0; }
            .card { max-width: 480px; margin: 0 auto; background-color: #171520; border-radius: 16px; padding: 36px; border: 1px solid #282435; }
            .brand { font-size: 26px; font-weight: 800; color: #ff528a; text-align: center; margin-bottom: 24px; letter-spacing: -0.5px; }
            h1 { font-size: 20px; font-weight: 700; color: #ffffff; text-align: center; margin: 0 0 12px; }
            p { font-size: 15px; line-height: 1.6; color: #b8b4c5; text-align: center; margin: 0 0 24px; }
            .code-box { background-color: #201c2e; border: 2px dashed #ff528a; border-radius: 12px; padding: 20px; text-align: center; margin: 0 0 24px; }
            .code { font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #ff528a; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
            .expiry { font-size: 13px; color: #8e899f; text-align: center; margin: 0 0 8px; }
            .footer { font-size: 12px; color: #585368; text-align: center; margin-top: 32px; border-top: 1px solid #282435; padding-top: 16px; }
          </style>
        </head>
        <body>
          <div class="card">
            <div class="brand">MuscleCuties</div>
            <h1>Confirm Your Email</h1>
            <p>Welcome! Enter the verification code below to activate your account and start your personalized journey.</p>
            <div class="code-box">
              <span class="code">{{code}}</span>
            </div>
            <p class="expiry">This code will expire in 10 minutes.</p>
            <p class="expiry">If you did not sign up for MuscleCuties, you can safely ignore this email.</p>
            <div class="footer">&copy; MuscleCuties. All rights reserved.</div>
          </div>
        </body>
        </html>
        """;

    private static string BuildPasswordResetText(string code) =>
        $"""
        MuscleCuties Password Reset

        Your 6-digit recovery code is: {code}

        This code expires in 10 minutes. Enter it in the app to reset your password.
        If you did not request a password reset, please secure your account immediately.
        """;

    private static string BuildPasswordResetHtml(string code) =>
        $$"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>Reset Your Password</title>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #0c0b10; color: #f4f3f7; padding: 40px 20px; margin: 0; }
            .card { max-width: 480px; margin: 0 auto; background-color: #171520; border-radius: 16px; padding: 36px; border: 1px solid #282435; }
            .brand { font-size: 26px; font-weight: 800; color: #ff528a; text-align: center; margin-bottom: 24px; letter-spacing: -0.5px; }
            h1 { font-size: 20px; font-weight: 700; color: #ffffff; text-align: center; margin: 0 0 12px; }
            p { font-size: 15px; line-height: 1.6; color: #b8b4c5; text-align: center; margin: 0 0 24px; }
            .code-box { background-color: #201c2e; border: 2px dashed #ff528a; border-radius: 12px; padding: 20px; text-align: center; margin: 0 0 24px; }
            .code { font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #ff528a; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
            .expiry { font-size: 13px; color: #8e899f; text-align: center; margin: 0 0 8px; }
            .footer { font-size: 12px; color: #585368; text-align: center; margin-top: 32px; border-top: 1px solid #282435; padding-top: 16px; }
          </style>
        </head>
        <body>
          <div class="card">
            <div class="brand">MuscleCuties</div>
            <h1>Reset Your Password</h1>
            <p>We received a request to reset your password. Enter the 6-digit recovery code below to proceed.</p>
            <div class="code-box">
              <span class="code">{{code}}</span>
            </div>
            <p class="expiry">This code will expire in 10 minutes.</p>
            <p class="expiry">If you did not request a password reset, please secure your account immediately.</p>
            <div class="footer">&copy; MuscleCuties. All rights reserved.</div>
          </div>
        </body>
        </html>
        """;

    private sealed class ResendEmailPayload
    {
        [JsonPropertyName("from")]
        public required string From { get; init; }

        [JsonPropertyName("to")]
        public required string[] To { get; init; }

        [JsonPropertyName("subject")]
        public required string Subject { get; init; }

        [JsonPropertyName("html")]
        public required string Html { get; init; }

        [JsonPropertyName("text")]
        public required string Text { get; init; }
    }
}
