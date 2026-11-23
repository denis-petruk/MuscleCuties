using System.Collections.Concurrent;
using System.Text.Json;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Nutrition.Planning;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;
using MuscleCuties.UseCases.Features.Account.Otp;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;
using MuscleCuties.UseCases.Features.Account.Recovery;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;
using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;
using MuscleCuties.UseCases.Features.Profile.Overview;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Archive;


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
