using MuscleCuties.Contracts.Account;

namespace MuscleCuties.Core.Services.Auth;

public enum EmailChallengePurpose
{
    Registration = 1,
    PasswordRecovery = 2
}

public sealed record EmailChallengeStartResult(
    bool Succeeded,
    string? ChallengeId,
    int ResendCooldownSeconds,
    string? ErrorMessage = null);

public sealed record EmailChallengeVerifyResult(
    bool Succeeded,
    string? VerificationToken,
    string? ErrorMessage = null);

