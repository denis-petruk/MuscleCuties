using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Account.SignIn;

namespace MuscleCuties.Infrastructure.Account.SignIn;

/// <summary>The account sign-in through the backend, which is the only place a password is exchanged for a session.</summary>
public sealed class CloudAccountSignInStore(
    MobileJourneyCloudGateway cloudGateway) : IAccountSignInStore
{
    /// <summary>Signs in against the backend with email and password.</summary>
    public async Task<SignedInAccount?> SignInWithPasswordAsync(
        string emailOrUsername,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await cloudGateway.SignInWithPasswordAsync(emailOrUsername, password, cancellationToken).ConfigureAwait(false);
        if (result.Status == BackendApiStatus.Success && result.Data is not null)
        {
            return new SignedInAccount(result.Data.IsOnboardingComplete);
        }

        if (result.Status == BackendApiStatus.Unauthorized)
        {
            return null;
        }

        throw new HttpRequestException($"Password sign-in failed with status {result.Status}.");
    }

    /// <summary>Signs in with an Apple identity token against the backend.</summary>
    public async Task<PlatformAccountOutcome> SignInWithPlatformAsync(
        string provider,
        string userIdentifier,
        string? email,
        string? fullName,
        string? identityToken,
        string? nonce,
        string? nonceChallengeId,
        CancellationToken cancellationToken = default,
        string? authorizationCode = null)
    {
        if (!string.Equals(provider, "apple", StringComparison.OrdinalIgnoreCase))
            return new PlatformAccountOutcome(false, false, false, "This sign in provider is unavailable.");

        var response = await cloudGateway.SignInWithAppleAsync(identityToken ?? string.Empty,
            nonce ?? string.Empty, nonceChallengeId ?? string.Empty, cancellationToken, authorizationCode).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Success && response.Data is not null)
            return new PlatformAccountOutcome(
                Succeeded: true,
                IsPendingOnboarding: !response.Data.IsOnboardingComplete,
                IsOnboardingComplete: response.Data.IsOnboardingComplete);

        var message = response.Status switch
        {
            BackendApiStatus.ValidationFailed => "Apple sign in could not be verified. Please try again.",
            BackendApiStatus.OfflineRejected or BackendApiStatus.Timeout or BackendApiStatus.ServerError or BackendApiStatus.RateLimited
                => "Apple sign in could not finish. Check your connection and try again.",
            _ => "Apple sign in could not finish. Sign in with email if you already have an account."
        };

        return new PlatformAccountOutcome(false, false, false, message);
    }
}
