using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Account.SignIn;

namespace MuscleCuties.Infrastructure.Account.SignIn;

// Transitional adapter: the old authentication service keeps local credential and session behavior
// until its repository implementation moves to Infrastructure in the final cleanup phase.
public sealed class LegacyAccountSignInStore(IServiceScopeFactory scopeFactory) : IAccountSignInStore
{
    public async Task<SignedInAccount?> SignInWithPasswordAsync(
        string emailOrUsername,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .LoginAsync(emailOrUsername, password).ConfigureAwait(false);
        return user is null ? null : new SignedInAccount(user.IsOnboardingComplete);
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
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        if (string.Equals(provider, "apple", StringComparison.OrdinalIgnoreCase))
        {
            var outcome = await auth.HandleAppleSignInAsync(new AppleSignInResult(
                userIdentifier, email, fullName, identityToken, nonce, nonceChallengeId, authorizationCode)).ConfigureAwait(false);
            return new PlatformAccountOutcome(
                outcome.Succeeded,
                outcome.IsPendingOnboarding,
                outcome.User?.IsOnboardingComplete ?? false,
                outcome.ErrorMessage);
        }

        var user = await auth.SignInWithExternalProviderAsync(new PlatformSignInResult(
            provider, userIdentifier, email, fullName, identityToken, nonce, nonceChallengeId, authorizationCode)).ConfigureAwait(false);
        return user is null
            ? new PlatformAccountOutcome(false, false, false)
            : new PlatformAccountOutcome(true, false, user.IsOnboardingComplete);
    }
}
