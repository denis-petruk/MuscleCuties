

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>Password recovery through the backend: the three steps and the session they produce.</summary>
public sealed class CloudAccountRecoveryStore(
    IMuscleCutiesBackendApiClient apiClient) : IAccountRecoveryStore
{
    private const int PasswordRecoveryChallengePurpose = 2;
    private readonly ConcurrentDictionary<string, string> _challengeByEmail = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Asks for a recovery code for an identifier, without revealing whether the account exists.</summary>
    public async Task<AccountRecoveryStartResult> StartAsync(string emailOrUsername, CancellationToken cancellationToken)
    {
        var email = emailOrUsername?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AuthInputValidator.IsValidEmail(email))
            return new AccountRecoveryStartResult(false, null, null, null, 0,
                "Enter the email address for your account.");

        var response = await apiClient.SendMutationAsync<object, CloudEmailChallengeStart>(
            HttpMethod.Post,
            "/api/v1/email-challenges/start",
            new { email, purpose = PasswordRecoveryChallengePurpose },
            idempotencyKey: null,
            cancellationToken).ConfigureAwait(false);

        if (response.Status == BackendApiStatus.Success &&
            Guid.TryParse(response.Data?.ChallengeId, out _))
        {
            _challengeByEmail[email] = response.Data.ChallengeId!;
            return new AccountRecoveryStartResult(true, response.Data.ChallengeId, email, email,
                response.Data.ResendCooldownSeconds);
        }

        return new AccountRecoveryStartResult(false, null, email, null, 0,
            response.ErrorCode ?? "Could not send recovery instructions.");
    }

    /// <summary>Resends the recovery code through the backend.</summary>
    public async Task<AccountRecoveryStartResult> ResendAsync(
        string challengeId, string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Guid.TryParse(challengeId, out _) || !AuthInputValidator.IsValidEmail(normalizedEmail))
            return new AccountRecoveryStartResult(false, null, normalizedEmail, null, 0,
                "Recovery session expired. Please restart password recovery.");

        var response = await apiClient.SendMutationAsync<object, CloudEmailChallengeStart>(
            HttpMethod.Post,
            "/api/v1/email-challenges/resend",
            new { challengeId, email = normalizedEmail, purpose = PasswordRecoveryChallengePurpose },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.Status != BackendApiStatus.Success ||
            !Guid.TryParse(response.Data?.ChallengeId, out _))
            return new AccountRecoveryStartResult(false, null, normalizedEmail, null, 0,
                response.ErrorCode ?? "Could not resend recovery code.");

        _challengeByEmail[normalizedEmail] = response.Data.ChallengeId!;
        return new AccountRecoveryStartResult(true, response.Data.ChallengeId, normalizedEmail, normalizedEmail,
            response.Data.ResendCooldownSeconds);
    }

    /// <summary>Checks the code and keeps the session it earns, which the reset step needs.</summary>
    public async Task<AccountRecoveryVerifyResult> VerifyAsync(string challengeId, string email, string code, CancellationToken cancellationToken)
    {
        var response = await apiClient.SendMutationAsync<object, CloudEmailChallengeVerification>(
            HttpMethod.Post,
            "/api/v1/email-challenges/verify",
            new { challengeId, email, code, purpose = PasswordRecoveryChallengePurpose },
            idempotencyKey: null,
            cancellationToken).ConfigureAwait(false);

        if (response.Status == BackendApiStatus.Success && response.Data?.Verified == true &&
            !string.IsNullOrWhiteSpace(response.Data.VerificationToken))
            return new AccountRecoveryVerifyResult(true, response.Data.VerificationToken, null);

        return new AccountRecoveryVerifyResult(false, null, response.ErrorCode ?? "Invalid recovery code.");
    }

    /// <summary>Sets the new password using the verified session, then drops that session.</summary>
    public async Task<AccountPasswordResetResult> ResetAsync(string email, string verificationToken, string newPassword, CancellationToken cancellationToken)
    {
        if (!_challengeByEmail.TryGetValue(email, out var challengeId) ||
            !Guid.TryParse(challengeId, out _) || string.IsNullOrWhiteSpace(verificationToken))
            return new AccountPasswordResetResult(false,
                "Recovery session expired. Please restart password recovery.");

        var response = await apiClient.SendMutationAsync<object, object>(
            HttpMethod.Post,
            "/api/v1/auth/password/reset",
            new { email, challengeId, verificationToken, newPassword },
            idempotencyKey: null,
            cancellationToken).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success
            ? new AccountPasswordResetResult(true, null)
            : new AccountPasswordResetResult(false, response.ErrorCode ?? "Password reset failed.");
    }

    /// <summary>Forgets the recovery session for an address, so an abandoned attempt cannot be resumed.</summary>
    public void ClearSession(string? email)
    {
        if (email is not null)
            _challengeByEmail.TryRemove(email, out _);
    }
}
