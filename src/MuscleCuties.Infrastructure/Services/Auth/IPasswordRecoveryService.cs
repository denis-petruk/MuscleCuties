namespace MuscleCuties.Core.Services.Auth;

public sealed record PasswordRecoveryStartResult(
    bool Succeeded,
    string? ChallengeId,
    string? Email,
    string? MaskedEmail,
    int ResendCooldownSeconds,
    string? ErrorMessage = null);

public sealed record PasswordRecoveryVerifyResult(
    bool Succeeded,
    string? VerificationToken,
    string? ErrorMessage = null);

public sealed record PasswordResetResult(
    bool Succeeded,
    string? ErrorMessage = null);

public interface IPasswordRecoveryService
{
    Task<PasswordRecoveryStartResult> StartRecoveryAsync(
        string emailOrUsername,
        CancellationToken cancellationToken = default);

    Task<PasswordRecoveryStartResult> ResendRecoveryCodeAsync(
        string challengeId,
        string email,
        CancellationToken cancellationToken = default);

    Task<PasswordRecoveryVerifyResult> VerifyRecoveryCodeAsync(
        string challengeId,
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(
        string email,
        string verificationToken,
        string newPassword,
        CancellationToken cancellationToken = default);

    void ClearRecoverySession(string? email = null);
}
