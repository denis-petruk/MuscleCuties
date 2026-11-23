

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudAccountRegistrationGateway(
    IPendingRegistrationService pending,
    IMuscleCutiesBackendApiClient apiClient) : IAccountRegistrationGateway
{
    private const int RegistrationChallengePurpose = 1;
    private const string ChallengeSendFailedMessage =
        "We couldn't send a verification code. Wait a moment, then tap Create account again.";
    private const string ChallengeRateLimitedMessage =
        "Too many code requests in the past 24 hours. Please try again later.";
    private const string ChallengeDeliveryUnavailableMessage =
        "Verification email is unavailable right now. Please try again later.";
    private readonly ConcurrentDictionary<Guid, PendingRegistrationData> _activeAttempts = new();

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetAsync<bool>(
            $"/api/v1/auth/email-exists?email={Uri.EscapeDataString(email)}",
            cancellationToken).ConfigureAwait(false);
        return result.Status == BackendApiStatus.Success && result.Data;
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetAsync<bool>(
            $"/api/v1/auth/username-exists?username={Uri.EscapeDataString(username)}",
            cancellationToken).ConfigureAwait(false);
        return result.Status == BackendApiStatus.Success && result.Data;
    }

    public async Task<RegistrationChallengeOutcome> StageAndStartChallengeAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken,
        RegistrationPrivacyConsent? privacyConsent = null)
    {
        var registration = pending.StartEmailRegistration(email, username, password, null,
            privacyConsent?.NoticeVersion, privacyConsent?.AcceptedAtUtc);
        var response = await apiClient.SendMutationAsync<object, CloudEmailChallengeStart>(
            HttpMethod.Post,
            "/api/v1/email-challenges/start",
            new { email, purpose = RegistrationChallengePurpose, privacyConsent },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.Status != BackendApiStatus.Success ||
            !Guid.TryParse(response.Data?.ChallengeId, out _))
        {
            var replaced = !pending.TryClear(registration);
            return new RegistrationChallengeOutcome(false, replaced,
                response.ResponseStatusCode switch
                {
                    System.Net.HttpStatusCode.TooManyRequests => ChallengeRateLimitedMessage,
                    System.Net.HttpStatusCode.ServiceUnavailable => ChallengeDeliveryUnavailableMessage,
                    _ => ChallengeSendFailedMessage
                });
        }

        if (!pending.TryUpdateChallengeId(registration, response.Data.ChallengeId!, out var updated) || updated is null)
            return new RegistrationChallengeOutcome(false, true);

        var attemptId = Guid.NewGuid();
        _activeAttempts[attemptId] = updated;
        return new RegistrationChallengeOutcome(true, false, null, attemptId);
    }

    public void CancelAttempt(Guid attemptId)
    {
        if (_activeAttempts.TryRemove(attemptId, out var registration))
            pending.TryClear(registration);
    }

    public void HandOffAttempt(Guid attemptId) => _activeAttempts.TryRemove(attemptId, out _);
}
