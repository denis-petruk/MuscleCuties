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

