namespace MuscleCuties.Core.Services.Auth;

public interface IAppleSignInService
{
    Task<AppleSignInResult?> SignInAsync(CancellationToken cancellationToken = default);
    Task<AppleCredentialState> GetCredentialStateAsync(string appleUserId);
}
