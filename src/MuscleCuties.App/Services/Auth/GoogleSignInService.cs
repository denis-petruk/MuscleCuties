using System.Diagnostics;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App.Services.Auth;

public sealed class GoogleSignInService : IPlatformSignInService
{
    public string ProviderName => "Google";
    public string LoginButtonText => "Log In with Google";
    public string RegisterButtonText => "Sign in with Google";

    public Task<PlatformSignInResult?> SignInWithProviderAsync(CancellationToken cancellationToken = default)
    {
        Trace.WriteLine("[Auth] Google sign-in is not yet configured. Requires Android Credential Manager setup.");
        return Task.FromResult<PlatformSignInResult?>(null);
    }
}
