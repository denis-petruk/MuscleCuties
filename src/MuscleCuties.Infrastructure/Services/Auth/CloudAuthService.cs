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
    /// <summary>Whether an address already has an account, which decides between sign-in and registration.</summary>
    public async Task<bool> EmailExistsAsync(string email)
    {
        var response = await apiClient.GetAsync<bool>($"/api/v1/auth/email-exists?email={Uri.EscapeDataString(email)}").ConfigureAwait(false);
        return response.Status == BackendApiStatus.Success && response.Data;
    }

    /// <summary>Whether a username is taken.</summary>
    public async Task<bool> UsernameExistsAsync(string username)
    {
        var response = await apiClient.GetAsync<bool>($"/api/v1/auth/username-exists?username={Uri.EscapeDataString(username)}").ConfigureAwait(false);
        return response.Status == BackendApiStatus.Success && response.Data;
    }

    /// <summary>Ends the session and clears what it left behind.</summary>
    public async Task LogoutAsync()
    {
        await cloudGateway.LogoutAndPurgeLocalStateAsync().ConfigureAwait(false);
    }

    /// <summary>Whether a usable session is held.</summary>
    public async Task<bool> IsLoggedInAsync()
    {
        var tokens = await tokenStore.ReadTokensAsync().ConfigureAwait(false);
        return tokens is not null && tokens.AccountId != Guid.Empty && !string.IsNullOrWhiteSpace(tokens.AccessToken);
    }

    /// <summary>The signed-in account's local id, which is what every store is keyed by.</summary>
    public async Task<int> GetCurrentUserIdAsync()
    {
        var tokens = await tokenStore.ReadTokensAsync().ConfigureAwait(false);
        return tokens is not null && tokens.AccountId != Guid.Empty
            ? ToLocalUserId(tokens.AccountId)
            : 0;
    }

    /// <summary>The signed-in account and its onboarding state, or nothing when there is no session.</summary>
    public async Task<CurrentUserState?> GetCurrentUserStateAsync()
    {
        var tokens = await tokenStore.ReadTokensAsync().ConfigureAwait(false);
        if (tokens is null || tokens.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(tokens.AccessToken))
            return null;

        return new CurrentUserState(ToLocalUserId(tokens.AccountId), tokens.IsOnboardingComplete);
    }

    /// <summary>The local id for an account id, derived so the same account always maps to the same number.</summary>
    private static int ToLocalUserId(Guid accountId)
    {
        var id = accountId.GetHashCode() & int.MaxValue;
        return id == 0 ? 1 : id;
    }
}
