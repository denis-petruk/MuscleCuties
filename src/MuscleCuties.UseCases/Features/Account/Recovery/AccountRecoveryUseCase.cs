using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Recovery;

public sealed class AccountRecoveryUseCase(IAccountRecoveryStore store) : IAccountRecoveryUseCase
{
    public Task<AccountRecoveryStartResult> StartAsync(
        StartAccountRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.StartAsync(request.EmailOrUsername, cancellationToken);
    }

    public Task<AccountRecoveryStartResult> ResendAsync(
        ResendAccountRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.ResendAsync(request.ChallengeId, request.Email, cancellationToken);
    }

    public Task<AccountRecoveryVerifyResult> VerifyAsync(
        VerifyAccountRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.VerifyAsync(request.ChallengeId, request.Email, request.Code, cancellationToken);
    }

    public Task<AccountPasswordResetResult> ResetAsync(
        ResetAccountPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.ResetAsync(request.Email, request.VerificationToken, request.NewPassword, cancellationToken);
    }

    public void ClearSession(string? email = null)
    {
        store.ClearSession(email);
    }
}
