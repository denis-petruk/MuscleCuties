global using StartAccountRegistrationRequest = MuscleCuties.Contracts.Account.StartAccountRegistrationRequest;
global using RegistrationPrivacyConsent = MuscleCuties.Contracts.Account.RegistrationPrivacyConsent;
global using PrivacyNotice = MuscleCuties.Contracts.Account.PrivacyNotice;
global using AccountRegistrationStatus = MuscleCuties.Contracts.Account.AccountRegistrationStatus;
global using StartAccountRegistrationResult = MuscleCuties.Contracts.Account.StartAccountRegistrationResult;

namespace MuscleCuties.UseCases.Features.Account.Registration.Contracts;

public interface IAccountRegistrationUseCase
{
    Task<StartAccountRegistrationResult> StartAsync(
        StartAccountRegistrationRequest request,
        CancellationToken cancellationToken = default);

    void CancelAttempt(Guid attemptId);
    void HandOffAttempt(Guid attemptId);
}
