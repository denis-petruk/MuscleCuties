namespace MuscleCuties.Core.Services.Auth;

/// <summary>The platform sign-in styles (Apple today) behind one contract.</summary>
public interface IPlatformSignInService
{
    string ProviderName { get; }
    string LoginButtonText { get; }
    string RegisterButtonText { get; }
    Task<PlatformSignInResult?> SignInWithProviderAsync(CancellationToken cancellationToken = default);
}
