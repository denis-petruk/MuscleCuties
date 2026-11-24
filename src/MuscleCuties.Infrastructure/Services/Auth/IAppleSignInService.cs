namespace MuscleCuties.Core.Services.Auth;

public interface IAppleSignInService
{
    Task<PlatformSignInResult?> SignInAsync(CancellationToken cancellationToken = default);
    Task<AppleCredentialState> GetCredentialStateAsync(string appleUserId);
}
