namespace MuscleCuties.UseCases.Features.Account.EmailChallenge;

public sealed record StartEmailChallengeRequest(
    string Email,
    int Purpose,
    MuscleCuties.UseCases.Features.Account.Registration.Contracts.RegistrationPrivacyConsent? PrivacyConsent = null);
public sealed record ResendEmailChallengeRequest(string ChallengeId, string Email, int Purpose);
public sealed record VerifyEmailChallengeRequest(string ChallengeId, string Email, string Code, int Purpose);
public sealed record EmailChallengeStartResult(bool Succeeded, string? ChallengeId, int ResendCooldownSeconds, int ExpiresInSeconds);
public sealed record EmailChallengeVerificationResult(bool Succeeded, string? VerificationToken, int ReceiptExpiresInSeconds);

public enum EmailChallengeStatus
{
    Succeeded,
    InvalidRequest,
    RateLimited,
    Cooldown,
    InvalidOrExpired,
    DeliveryUnavailable
}

public sealed record EmailChallengeOperationResult(
    EmailChallengeStatus Status,
    string? ChallengeId = null,
    int ResendCooldownSeconds = 0,
    int ExpiresInSeconds = 0,
    string? VerificationToken = null,
    int ReceiptExpiresInSeconds = 0);

public interface IEmailChallengeDelivery
{
    Task<bool> SendAsync(string normalizedEmail, string code, int purpose, CancellationToken cancellationToken);
}

public interface IEmailReceiptTokenIssuer
{
    string Issue(string normalizedEmail, int purpose, string challengeId, string receiptId, DateTimeOffset expiresAtUtc);
}

public interface IEmailChallengeUseCase
{
    Task<EmailChallengeOperationResult> StartAsync(StartEmailChallengeRequest request, CancellationToken cancellationToken);
    Task<EmailChallengeOperationResult> ResendAsync(ResendEmailChallengeRequest request, CancellationToken cancellationToken);
    Task<EmailChallengeOperationResult> VerifyAsync(VerifyEmailChallengeRequest request, CancellationToken cancellationToken);
}
