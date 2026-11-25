namespace MuscleCuties.Core.Services.Auth;

/// <summary>Apple sign-in as the app sees it: the credential, its state and the challenge nonce.</summary>
public interface IAppleSignInService
{
    Task<PlatformSignInResult?> SignInAsync(CancellationToken cancellationToken = default);
    Task<AppleCredentialState> GetCredentialStateAsync(string appleUserId);
}
