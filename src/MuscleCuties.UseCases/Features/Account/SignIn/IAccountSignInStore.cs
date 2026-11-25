namespace MuscleCuties.UseCases.Features.Account.SignIn;

/// <summary>An account that signed in, with its onboarding state.</summary>
public sealed record SignedInAccount(bool IsOnboardingComplete);

/// <summary>Whether a platform identity matched an account or needs onboarding.</summary>
public sealed record PlatformAccountOutcome(
    bool Succeeded,
    bool IsPendingOnboarding,
    bool IsOnboardingComplete,
    string? ErrorMessage = null);

/// <summary>The persistence sign-in needs.</summary>
public interface IAccountSignInStore
{
    Task<SignedInAccount?> SignInWithPasswordAsync(
        string emailOrUsername,
        string password,
        CancellationToken cancellationToken = default);

    Task<PlatformAccountOutcome> SignInWithPlatformAsync(
        string provider,
        string userIdentifier,
        string? email,
        string? fullName,
        string? identityToken,
        string? nonce,
        string? nonceChallengeId,
        CancellationToken cancellationToken = default,
        string? authorizationCode = null);
}
