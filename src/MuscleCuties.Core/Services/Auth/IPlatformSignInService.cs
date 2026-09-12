namespace MuscleCuties.Core.Services.Auth;

public interface IPlatformSignInService
{
    string ProviderName { get; }
    string LoginButtonText { get; }
    string RegisterButtonText { get; }
    Task<PlatformSignInResult?> SignInWithProviderAsync(CancellationToken cancellationToken = default);
}
