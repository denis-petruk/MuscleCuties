using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using MuscleCuties.UseCases.Features.Account.EmailChallenge;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;

/// <summary>
/// Narrow seam over the SES v2 client so the request composition can be verified without network calls
/// and without faking the entire AWS SDK surface.
/// </summary>
public interface ISesEmailTransport
{
    Task SendAsync(SendEmailRequest request, CancellationToken cancellationToken);
}

public sealed class AmazonSesEmailTransport(IAmazonSimpleEmailServiceV2 client) : ISesEmailTransport
{
    public Task SendAsync(SendEmailRequest request, CancellationToken cancellationToken) =>
        client.SendEmailAsync(request, cancellationToken);
}

/// <summary>
/// Delivers verification codes through AWS SES v2 using the Lambda execution role's credentials.
/// No long-lived provider API key is stored or read: authorization is the IAM permission granted to
/// the function role, and the sender identity is the SES-verified domain.
/// </summary>
public sealed class SesEmailChallengeDelivery(
    ISesEmailTransport transport,
    string? fromAddress,
    string? configurationSetName = null,
    ILogger<SesEmailChallengeDelivery>? logger = null) : IEmailChallengeDelivery
{
    private const int RegistrationPurpose = 1;
    private const int PasswordRecoveryPurpose = 2;
    private const int RequiredCodeLength = 6;
    private const string Utf8 = "UTF-8";

    public async Task<bool> SendAsync(
        string normalizedEmail,
        string code,
        int purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fromAddress) ||
            !IsPlausibleEmail(normalizedEmail) ||
            string.IsNullOrWhiteSpace(code) ||
            code.Length != RequiredCodeLength ||
            !code.All(char.IsAsciiDigit))
        {
            return false;
        }

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

        var request = new SendEmailRequest
        {
            FromEmailAddress = fromAddress,
            Destination = new Destination { ToAddresses = [normalizedEmail] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = Utf8 },
                    Body = new Body
                    {
                        Html = new Content { Data = htmlBody, Charset = Utf8 },
                        Text = new Content { Data = textBody, Charset = Utf8 }
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(configurationSetName))
            request.ConfigurationSetName = configurationSetName;

        try
        {
            await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonServiceException exception)
        {
            // A rejected send, e.g. the sandbox refusing an unverified recipient.
            LogFailure(exception);
            return false;
        }
        catch (AmazonClientException exception)
        {
            // SDK client failures (missing or unreachable credentials, endpoint resolution). Both
            // this and the service rejection above must degrade to "delivery unavailable", not a 500.
            LogFailure(exception);
            return false;
        }
        catch (HttpRequestException exception)
        {
            LogFailure(exception);
            return false;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure(exception);
            return false;
        }
    }

    /// <summary>
    /// Records why a code could not be delivered. Deliberately logs only the failure shape: the
    /// recipient address, the code and the message body stay out of logs.
    /// </summary>
    private void LogFailure(Exception exception)
    {
        logger?.LogWarning(
            "SES verification email was not sent ({ExceptionType}, error code {ErrorCode}).",
            exception.GetType().Name,
            exception is AmazonServiceException serviceException ? serviceException.ErrorCode : "none");
    }

    private static bool IsPlausibleEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254)
            return false;
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.IndexOf('@', at + 1) < 0 &&
               !value.Any(char.IsWhiteSpace);
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
}
