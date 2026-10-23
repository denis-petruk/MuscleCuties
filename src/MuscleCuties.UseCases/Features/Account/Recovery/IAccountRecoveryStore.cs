using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Recovery;

public interface IAccountRecoveryStore
{
    Task<AccountRecoveryStartResult> StartAsync(string emailOrUsername, CancellationToken cancellationToken);
    Task<AccountRecoveryStartResult> ResendAsync(string challengeId, string email, CancellationToken cancellationToken);

    Task<AccountRecoveryVerifyResult> VerifyAsync(string challengeId, string email, string code,
        CancellationToken cancellationToken);

    Task<AccountPasswordResetResult> ResetAsync(string email, string verificationToken, string newPassword,
        CancellationToken cancellationToken);

    void ClearSession(string? email);
}
