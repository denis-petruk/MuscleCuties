using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Recovery;

/// <summary>Password recovery: start, verify, reset, each step refusing an expired or reused session.</summary>
public sealed class AccountRecoveryUseCase(IAccountRecoveryStore store) : IAccountRecoveryUseCase
{
    /// <summary>Starts password recovery by sending a code to the account's email.</summary>
    public Task<AccountRecoveryStartResult> StartAsync(
        StartAccountRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.StartAsync(request.EmailOrUsername, cancellationToken);
    }

    /// <summary>Resends the recovery code when the cooldown allows it.</summary>
    public Task<AccountRecoveryStartResult> ResendAsync(
        ResendAccountRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.ResendAsync(request.ChallengeId, request.Email, cancellationToken);
    }

    /// <summary>Verifies the recovery code and issues the token that allows a reset.</summary>
    public Task<AccountRecoveryVerifyResult> VerifyAsync(
        VerifyAccountRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.VerifyAsync(request.ChallengeId, request.Email, request.Code, cancellationToken);
    }

    /// <summary>Resets the password using the verified recovery token.</summary>
    public Task<AccountPasswordResetResult> ResetAsync(
        ResetAccountPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.ResetAsync(request.Email, request.VerificationToken, request.NewPassword, cancellationToken);
    }

    /// <summary>Ends the verified recovery session for this address, so the reset token cannot be used again.</summary>
    public void ClearSession(string? email = null)
    {
        store.ClearSession(email);
    }
}
