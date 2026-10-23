using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Infrastructure.ApiClient;

namespace MuscleCuties.Core.Services.Auth;

/// <summary>
/// Cloud-authoritative authentication service implementing <see cref="IAuthService"/>
/// backed by <see cref="IBackendSessionTokenStore"/>, <see cref="MobileJourneyCloudGateway"/>,
/// and live ASP.NET Core API Gateway endpoints. Completely eliminates local SQLite session dependency.
/// </summary>
public sealed class CloudAuthService(
    IBackendSessionTokenStore tokenStore,
    MobileJourneyCloudGateway cloudGateway,
    IMuscleCutiesBackendApiClient apiClient) : IAuthService
{
    public async Task<User?> LoginAsync(string emailOrUsername, string password)
    {
        var response = await cloudGateway.SignInWithPasswordAsync(emailOrUsername, password).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Success && response.Data is not null)
        {
            return new User
            {
                Id = ToLocalUserId(response.Data.AccountId),
                Email = emailOrUsername,
                Username = emailOrUsername
            };
        }

        return null;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var response = await apiClient.GetAsync<bool>($"/api/v1/auth/email-exists?email={Uri.EscapeDataString(email)}").ConfigureAwait(false);
        return response.Status == BackendApiStatus.Success && response.Data;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        var response = await apiClient.GetAsync<bool>($"/api/v1/auth/username-exists?username={Uri.EscapeDataString(username)}").ConfigureAwait(false);
        return response.Status == BackendApiStatus.Success && response.Data;
    }

    public async Task<User?> SignInWithAppleAsync(AppleSignInResult appleAccount)
    {
        var outcome = await HandleAppleSignInAsync(appleAccount).ConfigureAwait(false);
        return outcome.User;
    }

    public async Task<AppleSignInOutcome> HandleAppleSignInAsync(AppleSignInResult appleAccount)
    {
        if (appleAccount is null || string.IsNullOrWhiteSpace(appleAccount.UserIdentifier))
            return new AppleSignInOutcome(false, false, null, false, "Apple account information was not received.");

        var response = await cloudGateway.SignInWithAppleAsync(
            appleAccount.IdentityToken ?? string.Empty,
            appleAccount.Nonce ?? string.Empty,
            appleAccount.NonceChallengeId ?? string.Empty,
            authorizationCode: appleAccount.AuthorizationCode).ConfigureAwait(false);
        if (response.Status != BackendApiStatus.Success || response.Data is null)
            return new AppleSignInOutcome(false, false, null, false,
                "Apple sign in could not be verified. Sign in with email if you already have an account.");

        var user = new User
        {
            Id = ToLocalUserId(response.Data.AccountId),
            Email = appleAccount.Email ?? string.Empty,
            Username = appleAccount.FullName ?? appleAccount.UserIdentifier
        };
        return new AppleSignInOutcome(true, true, user, false);
    }

    public async Task<User?> SignInWithExternalProviderAsync(PlatformSignInResult account)
    {
        if (account is null) return null;
        if (string.Equals(account.Provider, "apple", StringComparison.OrdinalIgnoreCase))
        {
            var outcome = await HandleAppleSignInAsync(new AppleSignInResult(
                account.UserIdentifier,
                account.Email,
                account.FullName,
                account.IdentityToken,
                account.Nonce,
                account.NonceChallengeId,
                account.AuthorizationCode)).ConfigureAwait(false);
            return outcome.User;
        }

        return null;
    }

    public async Task<PasswordOperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var response = await apiClient.SendMutationAsync<object, object>(
            HttpMethod.Post,
            "/api/v1/auth/password/change",
            new { currentPassword, newPassword }).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success
            ? new PasswordOperationResult(true)
            : new PasswordOperationResult(false, response.ErrorCode ?? "Could not change password.");
    }

    public async Task LogoutAsync()
    {
        await cloudGateway.LogoutAndPurgeLocalStateAsync().ConfigureAwait(false);
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var tokens = await tokenStore.ReadTokensAsync().ConfigureAwait(false);
        return tokens is not null && tokens.AccountId != Guid.Empty && !string.IsNullOrWhiteSpace(tokens.AccessToken);
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        var tokens = await tokenStore.ReadTokensAsync().ConfigureAwait(false);
        return tokens is not null && tokens.AccountId != Guid.Empty
            ? ToLocalUserId(tokens.AccountId)
            : 0;
    }

    public async Task<CurrentUserState?> GetCurrentUserStateAsync()
    {
        var tokens = await tokenStore.ReadTokensAsync().ConfigureAwait(false);
        if (tokens is null || tokens.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(tokens.AccessToken))
            return null;

        return new CurrentUserState(ToLocalUserId(tokens.AccountId), IsOnboardingComplete: true);
    }

    private static int ToLocalUserId(Guid accountId)
    {
        var id = accountId.GetHashCode() & int.MaxValue;
        return id == 0 ? 1 : id;
    }

    public Task SetCurrentUserAsync(User user)
    {
        return Task.CompletedTask;
    }
}
