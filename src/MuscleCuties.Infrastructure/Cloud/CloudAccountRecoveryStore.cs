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
