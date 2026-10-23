namespace MuscleCuties.UseCases.Features.Account.Otp.Contracts;

public sealed record RegistrationOtpSession(Guid SessionId, string Email, bool IsActive);

public sealed record VerifyRegistrationOtpRequest(Guid SessionId, string Code);

public sealed record ResendRegistrationOtpRequest(Guid SessionId);

public sealed record VerifyRegistrationOtpResult(bool Verified, bool Expired, string? ErrorMessage = null);

public sealed record ResendRegistrationOtpResult(
    bool Resent,
    bool Expired,
    int ResendCooldownSeconds = 60,
    string? ErrorMessage = null);

public interface IRegistrationOtpUseCase
{
    RegistrationOtpSession OpenSession();

    Task<VerifyRegistrationOtpResult> VerifyAsync(
        VerifyRegistrationOtpRequest request, CancellationToken cancellationToken = default);

    bool ConfirmSession(Guid sessionId);

    Task<ResendRegistrationOtpResult> ResendAsync(
        ResendRegistrationOtpRequest request, CancellationToken cancellationToken = default);

    void CancelSession(Guid sessionId);
    void HandOffSession(Guid sessionId);
}
