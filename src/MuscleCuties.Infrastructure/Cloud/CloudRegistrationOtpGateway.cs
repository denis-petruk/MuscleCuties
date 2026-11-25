

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The registration code step through the backend.</summary>
public sealed class CloudRegistrationOtpGateway(
    IPendingRegistrationService pending,
    IMuscleCutiesBackendApiClient apiClient) : IRegistrationOtpGateway
{
    private const int RegistrationChallengePurpose = 1;
    private const int VerificationCodeLength = 6;
    private readonly ConcurrentDictionary<Guid, OtpSessionState> _sessions = new();

    /// <summary>What the gateway holds for an open code session: the pending registration and whether it was confirmed.</summary>
    private sealed record OtpSessionState(PendingRegistrationData Registration, string? VerificationToken = null);

    /// <summary>Starts a code session, which is what the pending registration is tied to.</summary>
    public RegistrationOtpSession OpenSession()
    {
        var reg = pending.CurrentRegistration;
        if (reg is null || reg.CredentialType != UserCredentialType.Password ||
            !Guid.TryParse(reg.ChallengeId, out _))
            return new RegistrationOtpSession(Guid.Empty, string.Empty, false);

        var sessionId = Guid.NewGuid();
        _sessions[sessionId] = new OtpSessionState(reg);
        return new RegistrationOtpSession(sessionId, reg.Email, true);
    }

    /// <summary>Checks a code against its session and marks the session confirmed when it matches.</summary>
    public async Task<VerifyRegistrationOtpResult> VerifyAsync(Guid sessionId, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != VerificationCodeLength ||
            !code.All(static digit => digit is >= '0' and <= '9'))
            return new VerifyRegistrationOtpResult(false, false, "Please enter a valid verification code.");

        if (!_sessions.TryGetValue(sessionId, out var session) ||
            !ReferenceEquals(pending.CurrentRegistration, session.Registration))
            return new VerifyRegistrationOtpResult(false, true, "Verification session expired. Please restart registration.");

        var response = await apiClient.SendMutationAsync<object, CloudEmailChallengeVerification>(
            HttpMethod.Post,
            "/api/v1/email-challenges/verify",
            new
            {
                challengeId = session.Registration.ChallengeId,
                email = session.Registration.Email,
                code,
                purpose = RegistrationChallengePurpose
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var token = response.Data?.VerificationToken;
        if (response.Status != BackendApiStatus.Success || response.Data?.Verified != true ||
            string.IsNullOrWhiteSpace(token))
            return new VerifyRegistrationOtpResult(false, response.Status == BackendApiStatus.NotFound,
                response.ErrorCode ?? "Invalid or expired verification code.");

        if (!pending.TryConfirmChallenge(session.Registration, token))
            return new VerifyRegistrationOtpResult(false, true, "Verification session expired. Please restart registration.");

        var confirmed = pending.CurrentRegistration;
        if (confirmed is null || !_sessions.TryUpdate(sessionId, new OtpSessionState(confirmed, token), session))
            return new VerifyRegistrationOtpResult(false, true, "Verification session expired. Please restart registration.");

        return new VerifyRegistrationOtpResult(true, false);
    }

    /// <summary>Marks a session confirmed, which is what lets the account be created afterwards.</summary>
    public bool ConfirmSession(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) &&
               !string.IsNullOrWhiteSpace(session.VerificationToken) &&
               ReferenceEquals(pending.CurrentRegistration, session.Registration) &&
               session.Registration.IsVerified &&
               string.Equals(session.Registration.VerificationToken, session.VerificationToken, StringComparison.Ordinal);
    }

    /// <summary>Sends a fresh code for the same pending registration.</summary>
    public async Task<ResendRegistrationOtpResult> ResendAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) ||
            !ReferenceEquals(pending.CurrentRegistration, session.Registration))
            return new ResendRegistrationOtpResult(false, true,
                ErrorMessage: "Verification session expired. Please restart registration.");

        var response = await apiClient.SendMutationAsync<object, CloudEmailChallengeStart>(
            HttpMethod.Post,
            "/api/v1/email-challenges/resend",
            new
            {
                challengeId = session.Registration.ChallengeId,
                email = session.Registration.Email,
                purpose = RegistrationChallengePurpose
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.Status != BackendApiStatus.Success ||
            !Guid.TryParse(response.Data?.ChallengeId, out _))
            return new ResendRegistrationOtpResult(false, false,
                ErrorMessage: response.ErrorCode ?? "Could not resend verification code.");

        if (!pending.TryUpdateChallengeId(session.Registration, response.Data.ChallengeId!, out var updated) ||
            updated is null || !_sessions.TryUpdate(sessionId, new OtpSessionState(updated), session))
            return new ResendRegistrationOtpResult(false, true,
                ErrorMessage: "Verification session expired. Please restart registration.");

        return new ResendRegistrationOtpResult(true, false, response.Data.ResendCooldownSeconds);
    }

    /// <summary>Drops the registration session.</summary>
    public void CancelSession(Guid sessionId) => _sessions.TryRemove(sessionId, out _);
    /// <summary>Keeps the registration session for the caller; nothing is handed off.</summary>
    public void HandOffSession(Guid sessionId) { }
}
