namespace MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

/// <summary>An identifier to start recovery for, answered without revealing whether it exists.</summary>
public sealed record StartAccountRecoveryRequest(string EmailOrUsername);

/// <summary>A request for a fresh recovery code.</summary>
public sealed record ResendAccountRecoveryRequest(string ChallengeId, string Email);

/// <summary>A recovery code presented for verification.</summary>
public sealed record VerifyAccountRecoveryRequest(string ChallengeId, string Email, string Code);

/// <summary>The new password, sent under the verified recovery session.</summary>
public sealed record ResetAccountPasswordRequest(string Email, string VerificationToken, string NewPassword);

/// <summary>The outcome of starting recovery, deliberately uniform so an unknown account is indistinguishable.</summary>
public sealed record AccountRecoveryStartResult(
    bool Succeeded,
    string? ChallengeId,
    string? Email,
    string? MaskedEmail,
    int ResendCooldownSeconds,
    string? ErrorMessage = null);

/// <summary>Whether the code was accepted, and the session it earned.</summary>
public sealed record AccountRecoveryVerifyResult(
    bool Succeeded,
    string? VerificationToken,
    string? ErrorMessage = null);

/// <summary>Whether the reset was applied.</summary>
public sealed record AccountPasswordResetResult(bool Succeeded, string? ErrorMessage = null);

/// <summary>The three recovery steps as the endpoints call them.</summary>
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
