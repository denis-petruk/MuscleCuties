using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Account.SignIn;

namespace MuscleCuties.Infrastructure.Account.SignIn;

public sealed class CloudAccountSignInStore(
    MobileJourneyCloudGateway cloudGateway) : IAccountSignInStore
{
    public async Task<SignedInAccount?> SignInWithPasswordAsync(
        string emailOrUsername,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await cloudGateway.SignInWithPasswordAsync(emailOrUsername, password, cancellationToken).ConfigureAwait(false);
        if (result.Status == BackendApiStatus.Success && result.Data is not null)
        {
            return new SignedInAccount(true);
        }

        return null;
    }

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
            return new PlatformAccountOutcome(true, false, true);

        return new PlatformAccountOutcome(
            false,
            false,
            false,
            response.Status == BackendApiStatus.ValidationFailed
                ? "Apple sign in could not be verified. Please try again."
                : "Apple sign in could not finish. Sign in with email if you already have an account.");
    }
}
