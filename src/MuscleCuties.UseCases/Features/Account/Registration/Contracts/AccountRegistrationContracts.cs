namespace MuscleCuties.UseCases.Features.Account.Registration.Contracts;

public sealed record StartAccountRegistrationRequest(
    string Email,
    string Username,
    string Password,
    string ConfirmPassword,
    RegistrationPrivacyConsent? PrivacyConsent = null);

public static class PrivacyNotice
{
    public const string CurrentVersion = "privacy-notice-2026-10-17";
}

public sealed record RegistrationPrivacyConsent(string NoticeVersion, bool Accepted, DateTimeOffset AcceptedAtUtc);

public enum AccountRegistrationStatus
{
    Started,
    ExistingEmail,
    InvalidEmail,
    InvalidUsername,
    UsernameTaken,
    InvalidPassword,
    ChallengeFailed,
    PrivacyConsentRequired,
    ReplacedOrCancelled
}

public sealed record StartAccountRegistrationResult(
    AccountRegistrationStatus Status,
    string? ErrorMessage = null,
    string? ExistingEmail = null,
    Guid? AttemptId = null);

public interface IAccountRegistrationUseCase
{
    Task<StartAccountRegistrationResult> StartAsync(
        StartAccountRegistrationRequest request,
        CancellationToken cancellationToken = default);

    void CancelAttempt(Guid attemptId);
    void HandOffAttempt(Guid attemptId);
}
