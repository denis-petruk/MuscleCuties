namespace MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

public sealed record StartAccountRecoveryRequest(string EmailOrUsername);

public sealed record ResendAccountRecoveryRequest(string ChallengeId, string Email);

public sealed record VerifyAccountRecoveryRequest(string ChallengeId, string Email, string Code);

public sealed record ResetAccountPasswordRequest(string Email, string VerificationToken, string NewPassword);

public sealed record AccountRecoveryStartResult(
    bool Succeeded,
    string? ChallengeId,
    string? Email,
    string? MaskedEmail,
    int ResendCooldownSeconds,
    string? ErrorMessage = null);

public sealed record AccountRecoveryVerifyResult(
    bool Succeeded,
    string? VerificationToken,
    string? ErrorMessage = null);

public sealed record AccountPasswordResetResult(bool Succeeded, string? ErrorMessage = null);

public interface IAccountRecoveryUseCase
{
    Task<AccountRecoveryStartResult> StartAsync(StartAccountRecoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountRecoveryStartResult> ResendAsync(ResendAccountRecoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountRecoveryVerifyResult> VerifyAsync(VerifyAccountRecoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountPasswordResetResult> ResetAsync(ResetAccountPasswordRequest request,
        CancellationToken cancellationToken = default);

    void ClearSession(string? email = null);
}
