using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.Core.Services.Auth;

public enum EmailChallengePurpose
{
    Registration = 1,
    PasswordRecovery = 2
}

public sealed record EmailChallengeStartResult(
    bool Succeeded,
    string? ChallengeId,
    int ResendCooldownSeconds,
    string? ErrorMessage = null);

public sealed record EmailChallengeVerifyResult(
    bool Succeeded,
    string? VerificationToken,
    string? ErrorMessage = null);

public interface IEmailChallengeService
{
    Task<EmailChallengeStartResult> StartChallengeAsync(
        string email,
        EmailChallengePurpose purpose,
        CancellationToken cancellationToken = default,
        RegistrationPrivacyConsent? privacyConsent = null);

    Task<EmailChallengeStartResult> ResendChallengeAsync(
        string challengeId,
        string email,
        EmailChallengePurpose purpose,
        CancellationToken cancellationToken = default);

    Task<EmailChallengeVerifyResult> VerifyChallengeAsync(
        string challengeId,
        string email,
        string code,
        EmailChallengePurpose purpose,
        CancellationToken cancellationToken = default);
}
