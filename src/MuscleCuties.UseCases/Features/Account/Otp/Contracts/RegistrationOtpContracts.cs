namespace MuscleCuties.UseCases.Features.Account.Otp.Contracts;

/// <summary>A pending registration tied to the code session that will confirm it.</summary>
public sealed record RegistrationOtpSession(Guid SessionId, string Email, bool IsActive);

/// <summary>A code presented for a pending registration.</summary>
public sealed record VerifyRegistrationOtpRequest(Guid SessionId, string Code);

/// <summary>A request for a fresh registration code.</summary>
public sealed record ResendRegistrationOtpRequest(Guid SessionId);

/// <summary>Whether the code confirmed the registration.</summary>
public sealed record VerifyRegistrationOtpResult(bool Verified, bool Expired, string? ErrorMessage = null);

/// <summary>Whether a fresh code was sent, and when another may be requested.</summary>
public sealed record ResendRegistrationOtpResult(
    bool Resent,
    bool Expired,
    int ResendCooldownSeconds = 60,
    string? ErrorMessage = null);

/// <summary>The registration code step.</summary>
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
