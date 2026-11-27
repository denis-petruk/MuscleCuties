global using AccountRegistrationStatus = MuscleCuties.Contracts.Account.AccountRegistrationStatus;
global using PrivacyNotice = MuscleCuties.Contracts.Account.PrivacyNotice;
global using RegistrationPrivacyConsent = MuscleCuties.Contracts.Account.RegistrationPrivacyConsent;
global using StartAccountRegistrationRequest = MuscleCuties.Contracts.Account.StartAccountRegistrationRequest;
global using StartAccountRegistrationResult = MuscleCuties.Contracts.Account.StartAccountRegistrationResult;

namespace MuscleCuties.UseCases.Features.Account.Registration.Contracts;

/// <summary>The registration step the endpoints expose.</summary>
public interface IAccountRegistrationUseCase
{
    Task<StartAccountRegistrationResult> StartAsync(
        StartAccountRegistrationRequest request,
        CancellationToken cancellationToken = default);

    void CancelAttempt(Guid attemptId);
    void HandOffAttempt(Guid attemptId);
}
