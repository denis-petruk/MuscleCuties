namespace MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

/// <summary>An address and password for sign-in.</summary>
public sealed record PasswordSignInRequest(string EmailOrUsername, string Password);

/// <summary>A platform identity presented for sign-in.</summary>
public sealed record PlatformSignInRequest(
    string Provider,
    string UserIdentifier,
    string? Email,
    string? FullName,
    string? IdentityToken = null,
    string? Nonce = null,
    string? NonceChallengeId = null,
    string? AuthorizationCode = null);

/// <summary>Who signed in and whether onboarding still has to run.</summary>
public sealed record AccountSignInResult(
    bool Succeeded,
    bool IsOnboardingComplete = false,
    bool IsPendingOnboarding = false,
    string? ErrorMessage = null);

/// <summary>Sign-in as the endpoints call it.</summary>
public interface IAccountSignInUseCase
{
    Task<AccountSignInResult> SignInWithPasswordAsync(
        PasswordSignInRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountSignInResult> SignInWithPlatformAsync(
        PlatformSignInRequest request,
        CancellationToken cancellationToken = default);
}
