using MuscleCuties.UseCases.Features.Account.Otp.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Otp;

/// <summary>Verifies a registration code and marks the pending registration confirmed.</summary>
public sealed class RegistrationOtpUseCase(IRegistrationOtpGateway gateway) : IRegistrationOtpUseCase
{
    /// <summary>Opens a pending registration code session and returns it so the caller can send the code.</summary>
    public RegistrationOtpSession OpenSession()
    {
        return gateway.OpenSession();
    }

    /// <summary>Verifies the emailed registration code and returns the token that proves it.</summary>
    public Task<VerifyRegistrationOtpResult> VerifyAsync(
        VerifyRegistrationOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = request.Code?.Trim().Replace(" ", "", StringComparison.Ordinal) ?? string.Empty;
        if (code.Length != 6 || !code.All(static digit => digit is >= '0' and <= '9'))
            return Task.FromResult(new VerifyRegistrationOtpResult(false, false,
                "Please enter a valid 6-digit verification code."));

        return gateway.VerifyAsync(request.SessionId, code, cancellationToken);
    }

    /// <summary>Marks the session confirmed; a second confirmation returns false because the code is single use.</summary>
    public bool ConfirmSession(Guid sessionId)
    {
        return gateway.ConfirmSession(sessionId);
    }

    /// <summary>Sends the registration code again once the cooldown has passed.</summary>
    public Task<ResendRegistrationOtpResult> ResendAsync(
        ResendRegistrationOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return gateway.ResendAsync(request.SessionId, cancellationToken);
    }

    /// <summary>Drops the session without confirming it, which invalidates any code already sent.</summary>
    public void CancelSession(Guid sessionId)
    {
        gateway.CancelSession(sessionId);
    }

    /// <summary>Stops owning the session so another flow can continue it, without cancelling the code.</summary>
    public void HandOffSession(Guid sessionId)
    {
        gateway.HandOffSession(sessionId);
    }
}
