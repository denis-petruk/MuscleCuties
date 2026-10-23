namespace MuscleCuties.UseCases.Features.Account.SignIn;

public sealed record SignedInAccount(bool IsOnboardingComplete);

public sealed record PlatformAccountOutcome(
    bool Succeeded,
    bool IsPendingOnboarding,
    bool IsOnboardingComplete,
    string? ErrorMessage = null);

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
