using System.Collections.Concurrent;
using System.Text.Json;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
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
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle.Overview;
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

internal sealed record CloudEmailChallengeStart(string? ChallengeId, int ResendCooldownSeconds, int ExpiresInSeconds);
internal sealed record CloudEmailChallengeVerification(bool Verified, string? VerificationToken, int ReceiptExpiresInSeconds);
public sealed record CloudProfileWriteResponse(int? Version);

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

public sealed class CloudAccountRecoveryStore(
    IMuscleCutiesBackendApiClient apiClient) : IAccountRecoveryStore
{
    private const int PasswordRecoveryChallengePurpose = 2;
    private readonly ConcurrentDictionary<string, string> _challengeByEmail = new(StringComparer.OrdinalIgnoreCase);

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

    public void ClearSession(string? email)
    {
        if (email is not null)
            _challengeByEmail.TryRemove(email, out _);
    }
}

public sealed class CloudProfileSetupStore(
    IPendingRegistrationService pending) : IProfileSetupStore, IProfileSetupPersistence
{
    private const int DefaultCycleLengthDays = 28;

    private static ProfileSetupDraft Map(StagedProfileData staged) =>
        new(staged.Name, staged.BirthDate, staged.Height, staged.Weight,
            staged.ProfileImagePath, staged.WorkoutDaysPerWeek, staged.CycleLength,
            staged.SelectedActivities.Select(a => (int)a).ToArray(), staged.UseMetricSystem);

    private static StagedProfileData Map(ProfileSetupDraft draft) =>
        new(draft.Name.Trim(), draft.BirthDate, draft.HeightCm, draft.WeightKg,
            draft.ProfileImagePath.Trim(), draft.WorkoutDaysPerWeek,
            draft.CycleLength > 0 ? draft.CycleLength : DefaultCycleLengthDays,
            draft.SelectedActivityTypes.Where(v => Enum.IsDefined(typeof(WorkoutActivityType), v)).Select(v => (WorkoutActivityType)v).ToHashSet(),
            draft.UseMetricSystem);

    public Task<ProfileSetupLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var reg = pending.CurrentRegistration;
        if (reg is not null)
        {
            return Task.FromResult(new ProfileSetupLoadResult(
                true,
                reg.Username,
                pending.StagedProfile is { } staged ? Map(staged) : null));
        }

        return Task.FromResult(new ProfileSetupLoadResult(false, null, null));
    }

    public Task<bool> SaveAndPrepareQuizAsync(ProfileSetupDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pending.CurrentRegistration is null)
            return Task.FromResult(false);
        pending.StageProfile(Map(draft));
        return Task.FromResult(true);
    }

    public Task<ProfileSetupDraft?> LoadExistingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Cloud profile setup cannot load an existing account draft from registration staging.");
    }

    public Task<bool> SaveExistingWithSnapshotAsync(ProfileSetupDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Cloud profile setup cannot persist existing account edits through registration staging.");
    }
}

public sealed class CloudOnboardingQuizStore(
    IPendingRegistrationService pending,
    MobileJourneyCloudGateway cloudGateway,
    IMuscleCutiesBackendApiClient apiClient) : IOnboardingQuizStore
{
    private const string RegistrationQuizRoute = "/api/v1/accounts/registration/quiz";
    private const string RegistrationQuizUnavailableMessage =
        "Registration questions are temporarily unavailable. Please try again.";
    private const string RegistrationConflictCode = "registrationConflict";
    private const string PrivacyConsentRequiredCode = "privacyConsentRequired";
    private const string ConflictingCycleLengthsCode = "conflictingCycleLengths";

    public async Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(bool refresh,
        CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<QuizQuestionDto[]>(RegistrationQuizRoute,
            cancellationToken).ConfigureAwait(false);
        if (response.Status != BackendApiStatus.Success || response.Data is not { Length: > 0 })
            throw new InvalidOperationException(RegistrationQuizUnavailableMessage);

        return response.Data;
    }

    public async Task<QuizCommitResult> CommitAsync(
        IReadOnlyList<QuizSelectionDto> selections,
        CancellationToken cancellationToken)
    {
        var reg = pending.CurrentRegistration;
        var profile = pending.StagedProfile;
        if (reg is not null && profile is not null)
        {
            if (reg.CredentialType != UserCredentialType.Password ||
                string.IsNullOrWhiteSpace(reg.Password) ||
                !reg.IsVerified || !Guid.TryParse(reg.ChallengeId, out _) ||
                string.IsNullOrWhiteSpace(reg.VerificationToken) ||
                string.IsNullOrWhiteSpace(reg.PrivacyNoticeVersion) ||
                reg.PrivacyConsentAcceptedAtUtc is null)
                return new QuizCommitResult(false, true, "Complete email verification before creating an account.");

            var request = new
            {
                email = reg.Email,
                username = reg.Username,
                password = reg.Password,
                emailChallengeId = reg.ChallengeId,
                emailVerificationToken = reg.VerificationToken,
                privacyConsent = new
                {
                    noticeVersion = reg.PrivacyNoticeVersion,
                    accepted = true,
                    acceptedAtUtc = reg.PrivacyConsentAcceptedAtUtc
                },
                profile = new
                {
                    name = profile.Name,
                    birthDate = DateOnly.FromDateTime(profile.BirthDate),
                    heightCm = (decimal)profile.Height,
                    weightKg = (decimal)profile.Weight,
                    workoutDaysPerWeek = profile.WorkoutDaysPerWeek,
                    cycleLengthDays = profile.CycleLength,
                    selectedActivityTypes = profile.SelectedActivities.Select(a => (int)a).ToList(),
                    useMetricSystem = profile.UseMetricSystem
                },
                quizResponses = selections.Select(s => new { questionId = s.QuestionId, answerId = s.AnswerId }).ToList()
            };

            var response = await apiClient.SendMutationAsync<object, JsonElement>(
                HttpMethod.Post,
                "/api/v1/accounts/registration",
                request,
                idempotencyKey: Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);

            if (response.Status == BackendApiStatus.Success)
            {
                var signIn = await cloudGateway.SignInWithPasswordAsync(reg.Email, reg.Password!,
                    cancellationToken).ConfigureAwait(false);
                pending.Clear();
                return signIn.Status == BackendApiStatus.Success && signIn.Data is not null
                    ? new QuizCommitResult(true, false, null)
                    : new QuizCommitResult(true, true,
                        "Your account was created. Please sign in to continue.");
            }

            return new QuizCommitResult(false, true, GetRegistrationFailureMessage(response.Status, response.ErrorCode));
        }

        return new QuizCommitResult(false, false,
            "Your registration session expired. Please start again.");
    }

    private static string GetRegistrationFailureMessage(BackendApiStatus status, string? errorCode)
    {
        if (status == BackendApiStatus.OfflineRejected)
            return "You're offline. Reconnect and try again.";
        if (status == BackendApiStatus.Unauthorized)
            return "Your email verification expired. Start registration again to get a new code.";
        if (status == BackendApiStatus.Conflict)
            return errorCode == RegistrationConflictCode
                ? "This email or username is already in use. Try signing in or choose different details."
                : "Your account could not be saved. Please try again.";

        if (status == BackendApiStatus.ValidationFailed)
        {
            return errorCode switch
            {
                nameof(CompleteAccountRegistrationStatus.InvalidEmail) =>
                    "Check your email address and start registration again.",
                nameof(CompleteAccountRegistrationStatus.InvalidUsername) =>
                    "Check your username and start registration again.",
                nameof(CompleteAccountRegistrationStatus.InvalidPassword) =>
                    "Check your password and start registration again.",
                nameof(CompleteAccountRegistrationStatus.InvalidProfile) or ConflictingCycleLengthsCode =>
                    "Check your profile details, then try again.",
                nameof(CompleteAccountRegistrationStatus.InvalidQuiz) =>
                    "We couldn't save your preferences. Please try again later.",
                nameof(CompleteAccountRegistrationStatus.InvalidWorkoutPlan) =>
                    "Your workout plan could not be prepared. Please try again.",
                PrivacyConsentRequiredCode =>
                    "Privacy consent needs to be renewed. Start registration again.",
                _ => "We couldn't finish registration. Please try again."
            };
        }

        return "We couldn't finish registration. Please try again.";
    }
}

public sealed class CloudProfileOverviewStore(
    IMuscleCutiesBackendApiClient apiClient,
    MobileJourneyCloudGateway cloudGateway) : IProfileOverviewStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string PhotosRoute = "/api/v1/photos";
    private const string WorkoutHistoryCountRoute = "/api/v1/workouts/history/count";
    private const string ActiveInjuriesRoute = "/api/v1/injuries?activeOnly=true";
    private const string PhotosLatestRoute = "/api/v1/photos?limit=1&offset=0";
    private const string CycleOverviewRoute = "/api/v1/cycle/overview";
    private const int DefaultCycleLengthDays = 28;
    private const int DefaultWorkoutDaysPerWeek = 3;

    public async Task<ProfileOverviewSource> LoadAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var profileTask = apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken);
        var dashboardTask = cloudGateway.GetDashboardOverviewAsync(today, cancellationToken);
        var cycleTask = apiClient.GetAsync<ServerCycleOverview>(
            $"{CycleOverviewRoute}?from={today:yyyy-MM-dd}&through={today:yyyy-MM-dd}&today={today:yyyy-MM-dd}",
            cancellationToken);
        var workoutCountTask = apiClient.GetAsync<WorkoutHistoryCountDto>(WorkoutHistoryCountRoute, cancellationToken);
        var injuriesTask = apiClient.GetAsync<InjuryObservationRecord[]>(ActiveInjuriesRoute, cancellationToken);
        var photosTask = apiClient.GetAsync<PhotoHistoryResponse>(PhotosLatestRoute, cancellationToken);

        await Task.WhenAll(profileTask, dashboardTask, cycleTask, workoutCountTask, injuriesTask, photosTask).ConfigureAwait(false);

        var profile = CloudStoreResponse.RequireData(await profileTask.ConfigureAwait(false), "profile overview profile");
        var dashboard = CloudStoreResponse.RequireData(await dashboardTask.ConfigureAwait(false), "profile overview dashboard");
        var cycleResponse = await cycleTask.ConfigureAwait(false);
        var completedSessions = CloudStoreResponse.RequireData(
            await workoutCountTask.ConfigureAwait(false), "profile overview workout count").Count;
        var activeInjuryCount = CloudStoreResponse.RequireData(
            await injuriesTask.ConfigureAwait(false), "profile overview injuries").Count(injury => injury.IsActive);
        var photos = CloudStoreResponse.RequireData(await photosTask.ConfigureAwait(false), "profile overview photos");
        var vaultPhotoCount = photos.TotalCount;
        var latestVaultPhotoPath = string.Empty;
        if (photos.Photos.Count > 0)
        {
            var latestPhoto = photos.Photos[0];
            var downloadResponse = await apiClient.GetAsync<PhotoDownloadUrlResponse>(
                $"{PhotosRoute}/{latestPhoto.Id:D}/download-url",
                cancellationToken).ConfigureAwait(false);
            if (downloadResponse.Status == BackendApiStatus.Success &&
                downloadResponse.Data is not null &&
                !string.IsNullOrWhiteSpace(downloadResponse.Data.PresignedDownloadUrl))
            {
                latestVaultPhotoPath = downloadResponse.Data.PresignedDownloadUrl;
            }
        }

        var profileImagePath = string.Empty;
        if (!string.IsNullOrWhiteSpace(profile?.ProfileImagePath))
        {
            if (Guid.TryParse(profile.ProfileImagePath, out var avatarPhotoId) && avatarPhotoId != Guid.Empty)
            {
                var avatarDownload = await apiClient.GetAsync<PhotoDownloadUrlResponse>(
                    $"{PhotosRoute}/{avatarPhotoId:D}/download-url",
                    cancellationToken).ConfigureAwait(false);
                if (avatarDownload.Status == BackendApiStatus.Success &&
                    avatarDownload.Data is not null &&
                    !string.IsNullOrWhiteSpace(avatarDownload.Data.PresignedDownloadUrl))
                {
                    profileImagePath = avatarDownload.Data.PresignedDownloadUrl;
                }
            }
        }

        var goal = profile is not null && Enum.TryParse<UserGoal>(profile.Goal, true, out var parsedGoal) &&
                   Enum.IsDefined(parsedGoal)
            ? parsedGoal
            : UserGoal.MaintainHealth;
        var pace = profile is not null && Enum.TryParse<WeightGoalPace>(profile.WeightGoalPace, true, out var parsedPace) &&
                   Enum.IsDefined(parsedPace)
            ? parsedPace
            : WeightGoalPace.Steady;
        var units = DisplayUnits.FromStored(
            profile?.UnitSystem,
            profile?.BodyWeightUnit,
            profile?.FoodMassUnit,
            profile?.HeightUnit,
            profile?.DistanceUnit,
            profile?.EnergyUnit);
        var predictedPhase = cycleResponse.Status == BackendApiStatus.Success &&
                             cycleResponse.Data is { } cycle &&
                             Enum.TryParse<CyclePhase>(cycle.Prediction.CurrentPhase, true, out var cyclePhase) &&
                             Enum.IsDefined(cyclePhase)
            ? cyclePhase
            : dashboard is not null &&
              Enum.TryParse<CyclePhase>(dashboard.CurrentPhase, true, out var dashboardPhase) &&
              Enum.IsDefined(dashboardPhase)
                ? dashboardPhase
                : CyclePhase.Unknown;

        var overview = new ProfileOverviewDto(
            Email: profile?.Email ?? string.Empty,
            MemberSince: profile?.CreatedAtUtc,
            Name: profile?.Name ?? dashboard?.DisplayName ?? string.Empty,
            Goal: goal,
            WeightGoalPace: pace,
            CycleDays: profile?.CycleLength is > 0 ? profile.CycleLength : (dashboard?.PredictedCycleLength is > 0 ? dashboard.PredictedCycleLength : DefaultCycleLengthDays),
            ProfileImagePath: profileImagePath,
            UnitSystem: units.UnitSystem,
            BodyWeightUnit: units.BodyWeightUnit,
            HeightUnit: units.HeightUnit,
            WorkoutDaysPerWeek: profile?.WorkoutDaysPerWeek is > 0 ? profile.WorkoutDaysPerWeek : DefaultWorkoutDaysPerWeek,
            CompletedSessions: completedSessions,
            WorkoutStreakDays: dashboard?.WorkoutStreakDays ?? 0,
            NutritionStreakDays: dashboard?.NutritionStreakDays ?? 0,
            CurrentPhase: predictedPhase,
            CyclesTracked: dashboard?.CycleCount ?? 0,
            IsHealthConnected: null,
            ActiveInjuryCount: activeInjuryCount,
            VaultPhotoCount: vaultPhotoCount,
            LatestVaultPhotoPath: latestVaultPhotoPath);

        return new ProfileOverviewSource(overview, predictedPhase, predictedPhase);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await cloudGateway.LogoutAndPurgeLocalStateAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> UpdateProfileImageAsync(string? imagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // A profile reference must only be set after a validated private-photo intent,
        // upload, commit, and first-party read URL have all succeeded.
        throw new NotSupportedException("Cloud profile image updates require a validated, account-scoped first-party upload contract.");
    }
}

public sealed class CloudProfilePersonalInfoStore(
    IMuscleCutiesBackendApiClient apiClient) : IProfilePersonalInfoStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string PasswordChangeRoute = "/api/v1/auth/password/change";
    private const int DefaultCycleLengthDays = 28;
    private const int DefaultAdultAgeYears = 25;

    public async Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        var snapshot = CloudStoreResponse.RequireData(response, "personal info profile");
        var birthDate = snapshot.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today.AddYears(-DefaultAdultAgeYears);
        var goal = Enum.TryParse<UserGoal>(snapshot.Goal, true, out var parsedGoal) && Enum.IsDefined(parsedGoal)
            ? (int)parsedGoal
            : (int)UserGoal.MaintainHealth;
        var pace = Enum.TryParse<WeightGoalPace>(snapshot.WeightGoalPace, true, out var parsedPace) && Enum.IsDefined(parsedPace)
            ? (int)parsedPace
            : (int)WeightGoalPace.Steady;
        var experience = Enum.TryParse<TrainingExperienceLevel>(snapshot.TrainingExperienceLevel, true, out var parsedExp) &&
                         Enum.IsDefined(parsedExp)
            ? (int)parsedExp
            : (int)TrainingExperienceLevel.Intermediate;
        var trackingMode = Enum.TryParse<CycleTrackingMode>(snapshot.CycleTrackingMode, true, out var parsedMode) &&
                           Enum.IsDefined(parsedMode)
            ? (int)parsedMode
            : (int)CycleTrackingMode.ManualPhaseLogging;
        var context = ReproductiveContextCodec.Parse(snapshot.ReproductiveContextJson);

        var profile = new PersonalInfoProfileDto(
            snapshot.Name,
            birthDate,
            (float)snapshot.Height,
            (float)snapshot.Weight,
            goal,
            pace,
            experience,
            trackingMode,
            snapshot.WorkoutDaysPerWeek,
            snapshot.CycleLength > 0 ? snapshot.CycleLength : DefaultCycleLengthDays,
            (int)context.Pregnancy,
            (int)context.Contraception);

        return new LoadPersonalInfoResult(snapshot.Email, profile);
    }

    public async Task<bool> SaveWithSnapshotAsync(SavePersonalInfoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var currentResponse = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        if (currentResponse.Status != BackendApiStatus.Success || currentResponse.Data is null)
            return false;

        var goal = Enum.IsDefined(typeof(UserGoal), request.Goal)
            ? ((UserGoal)request.Goal).ToString()
            : currentResponse.Data.Goal;
        var pace = Enum.IsDefined(typeof(WeightGoalPace), request.WeightGoalPace)
            ? ((WeightGoalPace)request.WeightGoalPace).ToString()
            : currentResponse.Data.WeightGoalPace;
        var experience = Enum.IsDefined(typeof(TrainingExperienceLevel), request.TrainingExperienceLevel)
            ? ((TrainingExperienceLevel)request.TrainingExperienceLevel).ToString()
            : currentResponse.Data.TrainingExperienceLevel;
        var trackingMode = Enum.IsDefined(typeof(CycleTrackingMode), request.CycleTrackingMode)
            ? ((CycleTrackingMode)request.CycleTrackingMode).ToString()
            : currentResponse.Data.CycleTrackingMode;
        var pregnancy = Enum.IsDefined(typeof(PregnancyStatus), request.PregnancyStatus)
            ? (PregnancyStatus)request.PregnancyStatus
            : PregnancyStatus.Unknown;
        var contraception = Enum.IsDefined(typeof(ContraceptionStatus), request.ContraceptionStatus)
            ? (ContraceptionStatus)request.ContraceptionStatus
            : ContraceptionStatus.Unknown;
        var reproductiveContextJson = ReproductiveContextCodec.Serialize(new ReproductiveContext(pregnancy, contraception));

        var response = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            ProfileRoute,
            new
            {
                expectedVersion = currentResponse.Data.Version,
                name = request.Name.Trim(),
                dateOfBirth = DateOnly.FromDateTime(request.BirthDate),
                setDateOfBirth = true,
                height = (decimal)request.Height,
                weight = (decimal)request.Weight,
                goal,
                weightGoalPace = pace,
                trainingExperienceLevel = experience,
                cycleTrackingMode = trackingMode,
                workoutDaysPerWeek = request.WorkoutDaysPerWeek,
                cycleLength = request.CycleLength,
                reproductiveContextJson
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success;
    }

    public async Task<ChangeAccountPasswordResult> ChangePasswordWithSnapshotAsync(
        ChangeAccountPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await apiClient.SendMutationAsync<object, object>(
            HttpMethod.Post,
            PasswordChangeRoute,
            new { currentPassword = request.CurrentPassword, newPassword = request.NewPassword },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success
            ? new ChangeAccountPasswordResult(true, "Password updated.")
            : new ChangeAccountPasswordResult(false, response.ErrorCode ?? "Could not change password.");
    }
}

public sealed class CloudProfileNutritionSettingsStore(
    IMuscleCutiesBackendApiClient apiClient,
    INutritionPlanner? planner = null) : IProfileNutritionSettingsStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string NutritionPreferencesRoute = "/api/v1/profile/nutrition-preferences";
    private const string NutritionTargetsRoute = "/api/v1/profile/nutrition-targets";
    private const string CycleOverviewRoute = "/api/v1/cycle/overview";
    private const int DefaultAdultAgeYears = 25;

    public async Task<NutritionSettingsProfileDto?> LoadAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var profileTask = apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken);
        var cycleTask = apiClient.GetAsync<ServerCycleOverview>(
            $"{CycleOverviewRoute}?from={today:yyyy-MM-dd}&through={today:yyyy-MM-dd}&today={today:yyyy-MM-dd}",
            cancellationToken);

        await Task.WhenAll(profileTask, cycleTask).ConfigureAwait(false);

        var profileResponse = await profileTask.ConfigureAwait(false);
        if (profileResponse.Status == BackendApiStatus.NotFound)
            return null;
        var snapshot = CloudStoreResponse.RequireData(profileResponse, "nutrition settings profile");
        var cycleResponse = await cycleTask.ConfigureAwait(false);
        var cycle = CloudStoreResponse.RequireData(cycleResponse, "nutrition settings cycle");
        var phase = Enum.TryParse<CyclePhase>(cycle.Prediction.CurrentPhase, true, out var parsedPhase) &&
                    Enum.IsDefined(parsedPhase)
            ? parsedPhase
            : CyclePhase.Unknown;

        var dob = snapshot.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today.AddYears(-DefaultAdultAgeYears);
        var goal = Enum.TryParse<UserGoal>(snapshot.Goal, true, out var g) && Enum.IsDefined(g)
            ? g
            : UserGoal.MaintainHealth;
        var pace = Enum.TryParse<WeightGoalPace>(snapshot.WeightGoalPace, true, out var p) && Enum.IsDefined(p)
            ? p
            : WeightGoalPace.Steady;
        var experience = Enum.TryParse<TrainingExperienceLevel>(snapshot.TrainingExperienceLevel, true, out var exp) &&
                         Enum.IsDefined(exp)
            ? exp
            : TrainingExperienceLevel.Intermediate;
        var savedGoals = NutritionGoalsDtoMapper.ToDto(ProfileNutritionGoalsJsonCodec.FromJson(snapshot.NutritionGoalsJson));

        return new NutritionSettingsProfileDto(
            dob,
            (float)snapshot.Height,
            (float)snapshot.Weight,
            experience,
            snapshot.WorkoutDaysPerWeek,
            goal,
            pace,
            snapshot.DietaryTags ?? string.Empty,
            savedGoals,
            phase);
    }

    public NutritionGoalsDto CalculateGoals(
        NutritionSettingsProfileDto profile, NutritionSettingsSelectionDto selection)
    {
        if (planner is null)
            return NutritionGoalsDto.Empty;

        var planningProfile = new UserProfile
        {
            Name = string.Empty,
            DateOfBirth = profile.DateOfBirth,
            Height = profile.Height,
            Weight = profile.Weight,
            TrainingExperienceLevel = profile.TrainingExperienceLevel,
            WorkoutDaysPerWeek = profile.WorkoutDaysPerWeek,
            Goal = selection.Goal,
            WeightGoalPace = selection.WeightGoalPace,
            DietaryTags = selection.DietaryTags,
            NutritionGoalsJson = string.Empty
        };

        var plan = planner.CreateDailyPlan(planningProfile, profile.Phase, DateTime.Today);
        return NutritionGoalsDtoMapper.ToDto(plan.Goals);
    }

    public async Task<bool> SaveAsync(
        NutritionSettingsSelectionDto selection, NutritionGoalsDto? goalOverrides, CancellationToken cancellationToken)
    {
        var currentResponse = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        if (currentResponse.Status != BackendApiStatus.Success || currentResponse.Data is null)
            return false;

        var prefResponse = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            NutritionPreferencesRoute,
            new
            {
                expectedVersion = currentResponse.Data.Version,
                goal = selection.Goal.ToString(),
                weightGoalPace = selection.WeightGoalPace.ToString(),
                dietaryTags = selection.DietaryTags ?? string.Empty
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        if (prefResponse.Status != BackendApiStatus.Success)
            return false;

        if (goalOverrides is not null)
        {
            var nextVersion = prefResponse.Data?.Version ?? (currentResponse.Data.Version + 1);
            var domainGoals = NutritionGoalsDtoMapper.ToDomain(goalOverrides);
            var serializedGoals = ProfileNutritionGoalsJsonCodec.ToJson(domainGoals);
            using var doc = JsonDocument.Parse(serializedGoals);
            var targets = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
                targets[prop.Name] = prop.Value.Clone();

            var targetsResponse = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
                HttpMethod.Put,
                NutritionTargetsRoute,
                new
                {
                    expectedVersion = nextVersion,
                    targets
                },
                idempotencyKey: Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);

            if (targetsResponse.Status != BackendApiStatus.Success)
                return false;
        }

        return true;
    }
}

public sealed class CloudProfileWorkoutPreferencesStore(
    IMuscleCutiesBackendApiClient apiClient) : IProfileWorkoutPreferencesStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string WorkoutPreferencesRoute = "/api/v1/profile/workout-preferences";
    private const int DefaultDaysPerWeek = 3;

    public async Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound)
            return null;
        var snapshot = CloudStoreResponse.RequireData(response, "workout preferences profile");
        var activities = WorkoutActivityPreferences.Parse(snapshot.PreferredWorkoutActivityTypes)
            .Select(activity => (int)activity)
            .ToArray();
        var strengthStyle = (int)WorkoutActivityPreferences.ParseStrengthStyle(snapshot.PreferredWorkoutActivityTypes);
        var days = snapshot.WorkoutDaysPerWeek > 0 ? snapshot.WorkoutDaysPerWeek : DefaultDaysPerWeek;

        return new WorkoutPreferencesDto(activities, strengthStyle, days, snapshot.EquipmentLevel);
    }

    public async Task<WorkoutPreferencesError> SaveAndUpdatePlanAsync(
        WorkoutPreferencesDto preferences, CancellationToken cancellationToken)
    {
        var currentResponse = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        if (currentResponse.Status == BackendApiStatus.NotFound || currentResponse.Data is null)
            return WorkoutPreferencesError.ProfileMissing;

        var response = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            WorkoutPreferencesRoute,
            new
            {
                expectedVersion = currentResponse.Data.Version,
                activityTypes = preferences.ActivityTypes,
                strengthStyle = preferences.StrengthStyle,
                daysPerWeek = preferences.DaysPerWeek,
                equipmentLevel = preferences.EquipmentLevel
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status switch
        {
            BackendApiStatus.Success => WorkoutPreferencesError.None,
            BackendApiStatus.NotFound => WorkoutPreferencesError.ProfileMissing,
            BackendApiStatus.ValidationFailed => WorkoutPreferencesError.InvalidPreferences,
            _ => WorkoutPreferencesError.PlanUpdateFailed
        };
    }
}

public sealed class CloudProfileUnitsStore(
    IMuscleCutiesBackendApiClient apiClient) : IProfileUnitsStore
{
    private const string ProfileUnitsRoute = "/api/v1/profile/units";
    private const int FallbackInitialVersion = 1;

    public async Task<DisplayUnits?> LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<ServerProfileUnitsSnapshot>(ProfileUnitsRoute, cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound)
            return null;
        var units = CloudStoreResponse.RequireData(response, "profile units");

        return DisplayUnits.FromInput(
            units.UseMetricSystem,
            units.BodyWeightUnit,
            units.FoodMassUnit,
            units.HeightUnit,
            units.DistanceUnit,
            units.EnergyUnit);
    }

    public async Task<bool> SaveCurrentWithSnapshotAsync(
        DisplayUnits settings,
        string snapshotReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var currentUnits = await apiClient.GetAsync<ServerProfileUnitsSnapshot>(ProfileUnitsRoute, cancellationToken).ConfigureAwait(false);
        var expectedVersion = currentUnits.Status == BackendApiStatus.NotFound
            ? FallbackInitialVersion
            : CloudStoreResponse.RequireData(currentUnits, "profile units before save").Version;

        var response = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            ProfileUnitsRoute,
            new
            {
                expectedVersion,
                useMetricSystem = settings.UseMetricSystem,
                bodyWeightUnit = settings.BodyWeightUnit,
                foodMassUnit = settings.FoodMassUnit,
                heightUnit = settings.HeightUnit,
                distanceUnit = settings.DistanceUnit,
                energyUnit = settings.EnergyUnit
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success;
    }
}

public sealed class CloudProfileAccountSession(
    IBackendSessionTokenStore tokenStore) : IProfileAccountSession
{
    public async Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        return tokens is not null && tokens.AccountId != Guid.Empty
            ? Math.Abs(tokens.AccountId.GetHashCode())
            : 0;
    }
}
