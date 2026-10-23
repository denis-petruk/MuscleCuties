namespace MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

public sealed record PasswordSignInRequest(string EmailOrUsername, string Password);

public sealed record PlatformSignInRequest(
    string Provider,
    string UserIdentifier,
    string? Email,
    string? FullName,
    string? IdentityToken = null,
    string? Nonce = null,
    string? NonceChallengeId = null,
    string? AuthorizationCode = null);

public sealed record AccountSignInResult(
    bool Succeeded,
    bool IsOnboardingComplete = false,
    bool IsPendingOnboarding = false,
    string? ErrorMessage = null);

public interface IAccountSignInUseCase
{
    Task<AccountSignInResult> SignInWithPasswordAsync(
        PasswordSignInRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountSignInResult> SignInWithPlatformAsync(
        PlatformSignInRequest request,
        CancellationToken cancellationToken = default);
}
