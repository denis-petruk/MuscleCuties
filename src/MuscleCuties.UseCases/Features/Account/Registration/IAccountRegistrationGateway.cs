using MuscleCuties.Contracts.Account;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Registration;

/// <summary>What the gateway produced for a registration: the challenge or a refusal.</summary>
public sealed record RegistrationChallengeOutcome(
    bool Started,
    bool ReplacedOrCancelled = false,
    string? ErrorMessage = null,
    Guid? AttemptId = null);

/// <summary>The checks and the code step behind registration.</summary>
public interface IAccountRegistrationGateway
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    Task<RegistrationChallengeOutcome> StageAndStartChallengeAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken,
        RegistrationPrivacyConsent? privacyConsent = null);

    void CancelAttempt(Guid attemptId);
    void HandOffAttempt(Guid attemptId);
}
