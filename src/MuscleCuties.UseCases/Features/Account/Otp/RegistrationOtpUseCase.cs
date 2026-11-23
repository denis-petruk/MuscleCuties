using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Otp;

public sealed class RegistrationOtpUseCase(IRegistrationOtpGateway gateway) : IRegistrationOtpUseCase
{
    public RegistrationOtpSession OpenSession()
    {
        return gateway.OpenSession();
    }

    public Task<VerifyRegistrationOtpResult> VerifyAsync(
        VerifyRegistrationOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!AuthInputValidator.TryValidateOtpCode(request.Code, out var code, out _))
            return Task.FromResult(new VerifyRegistrationOtpResult(false, false,
                AuthInputValidator.OtpCodeRequirementsMessage));

        return gateway.VerifyAsync(request.SessionId, code, cancellationToken);
    }

    public bool ConfirmSession(Guid sessionId)
    {
        return gateway.ConfirmSession(sessionId);
    }

    public Task<ResendRegistrationOtpResult> ResendAsync(
        ResendRegistrationOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return gateway.ResendAsync(request.SessionId, cancellationToken);
    }

    public void CancelSession(Guid sessionId)
    {
        gateway.CancelSession(sessionId);
    }

    public void HandOffSession(Guid sessionId)
    {
        gateway.HandOffSession(sessionId);
    }
}
