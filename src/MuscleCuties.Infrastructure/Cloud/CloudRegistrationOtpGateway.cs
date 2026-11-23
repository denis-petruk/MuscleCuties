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

public sealed class CloudRegistrationOtpGateway(
    IPendingRegistrationService pending,
    IMuscleCutiesBackendApiClient apiClient) : IRegistrationOtpGateway
{
    private const int RegistrationChallengePurpose = 1;
    private const int VerificationCodeLength = 6;
    private readonly ConcurrentDictionary<Guid, OtpSessionState> _sessions = new();

    private sealed record OtpSessionState(PendingRegistrationData Registration, string? VerificationToken = null);

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

    public bool ConfirmSession(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) &&
               !string.IsNullOrWhiteSpace(session.VerificationToken) &&
               ReferenceEquals(pending.CurrentRegistration, session.Registration) &&
               session.Registration.IsVerified &&
               string.Equals(session.Registration.VerificationToken, session.VerificationToken, StringComparison.Ordinal);
    }

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

    public void CancelSession(Guid sessionId) => _sessions.TryRemove(sessionId, out _);
    public void HandOffSession(Guid sessionId) { }
}
