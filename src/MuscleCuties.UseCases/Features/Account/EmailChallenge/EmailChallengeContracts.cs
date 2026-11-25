namespace MuscleCuties.UseCases.Features.Account.EmailChallenge;

/// <summary>A request for a verification code for one address and purpose.</summary>
public sealed record StartEmailChallengeRequest(
    string Email,
    int Purpose,
    MuscleCuties.Contracts.Account.RegistrationPrivacyConsent? PrivacyConsent = null);
/// <summary>A request for a fresh code on an existing challenge.</summary>
public sealed record ResendEmailChallengeRequest(string ChallengeId, string Email, int Purpose);
/// <summary>A code presented against its challenge.</summary>
public sealed record VerifyEmailChallengeRequest(string ChallengeId, string Email, string Code, int Purpose);

/// <summary>How a challenge attempt ended, so the client can distinguish a wrong code from an expired one.</summary>
public enum EmailChallengeStatus
{
    Succeeded,
    InvalidRequest,
    RateLimited,
    Cooldown,
    InvalidOrExpired,
    DeliveryUnavailable
}

/// <summary>The outcome of an email challenge operation, with the receipt when a code was sent.</summary>
public sealed record EmailChallengeOperationResult(
    EmailChallengeStatus Status,
    string? ChallengeId = null,
    int ResendCooldownSeconds = 0,
    int ExpiresInSeconds = 0,
    string? VerificationToken = null,
    int ReceiptExpiresInSeconds = 0);

/// <summary>Delivers a verification code; implementations must never log it.</summary>
public interface IEmailChallengeDelivery
{
    Task<bool> SendAsync(string normalizedEmail, string code, int purpose, CancellationToken cancellationToken);
}

/// <summary>Issues the signed receipt that proves a delivery happened.</summary>
public interface IEmailReceiptTokenIssuer
{
    string Issue(string normalizedEmail, int purpose, string challengeId, string receiptId, DateTimeOffset expiresAtUtc);
}

/// <summary>Starts and verifies email challenges.</summary>
public interface IEmailChallengeUseCase
{
    Task<EmailChallengeOperationResult> StartAsync(StartEmailChallengeRequest request, CancellationToken cancellationToken);
    Task<EmailChallengeOperationResult> ResendAsync(ResendEmailChallengeRequest request, CancellationToken cancellationToken);
    Task<EmailChallengeOperationResult> VerifyAsync(VerifyEmailChallengeRequest request, CancellationToken cancellationToken);
}
