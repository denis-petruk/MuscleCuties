using MuscleCuties.UseCases.Features.Account.Otp.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Otp;

public interface IRegistrationOtpGateway
{
    RegistrationOtpSession OpenSession();
    Task<VerifyRegistrationOtpResult> VerifyAsync(Guid sessionId, string code, CancellationToken cancellationToken);
    bool ConfirmSession(Guid sessionId);
    Task<ResendRegistrationOtpResult> ResendAsync(Guid sessionId, CancellationToken cancellationToken);
    void CancelSession(Guid sessionId);
    void HandOffSession(Guid sessionId);
}
