using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.Services.Auth;

/// <summary>Lets the auth screens offer a device-account sign-in without depending on the Apple SDK.</summary>
public interface IPlatformAccountSignInAdapter
{
    bool IsAvailable { get; }
    string ProviderName { get; }
    string LoginButtonText { get; }
    string RegisterButtonText { get; }
    Task<PlatformSignInRequest?> SignInAsync();
}

/// <summary>Wraps the platform provider when one is registered and reports it unavailable otherwise, so the</summary>
/// button can still render a sensible label.</summary>
public sealed class PlatformAccountSignInAdapter(IPlatformSignInService? platformSignInService)
    : IPlatformAccountSignInAdapter
{
    /// <summary>True when the platform can run its sign-in.</summary>
    public bool IsAvailable => platformSignInService is not null;
    /// <summary>Provider name, defaulted to Apple for builds where the provider is not registered.</summary>
    public string ProviderName => platformSignInService?.ProviderName ?? "Apple";
    /// <summary>Sign-in button label, with a provider-neutral fallback.</summary>
    public string LoginButtonText => platformSignInService?.LoginButtonText ?? "Continue with device account";
    /// <summary>Registration button label, with the same fallback.</summary>
    public string RegisterButtonText => platformSignInService?.RegisterButtonText ?? "Continue with device account";

    /// <summary>Runs the provider sign-in and maps its credential onto the request the use case accepts;</summary>
    /// null means the person cancelled.</summary>
    public async Task<PlatformSignInRequest?> SignInAsync()
    {
        var account = platformSignInService is null
            ? null
            : await platformSignInService.SignInWithProviderAsync();
        return account is null
            ? null
            : new PlatformSignInRequest(
                account.Provider, account.UserIdentifier, account.Email, account.FullName,
                account.IdentityToken, account.Nonce, account.NonceChallengeId, account.AuthorizationCode);
    }
}

/// <summary>The one device-account flow both auth screens run: availability, credential, exchange with the</summary>
/// server, then the success callback. Every failure ends in a message rather than an exception.</summary>
public static class PlatformSignInCoordinator
{
    /// <summary>Runs the platform sign-in and hands the account to the sign-in use case.</summary>
    public static async Task ExecuteAsync(
        IPlatformAccountSignInAdapter platformSignIn,
        IAccountSignInUseCase signIn,
        Func<bool, Task> onAccountSuccessAsync,
        Action<string> setErrorMessage,
        string callerName)
    {
        var providerName = platformSignIn.ProviderName;
        if (!platformSignIn.IsAvailable)
        {
            setErrorMessage("Apple sign in is not available in this build.");
            return;
        }

        try
        {
            var account = await platformSignIn.SignInAsync().ConfigureAwait(false);
            if (account is null)
                return;

            var result = await signIn.SignInWithPlatformAsync(account).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                setErrorMessage(result.ErrorMessage ?? $"{providerName} sign in could not finish. Check your connection and try again.");
                return;
            }

            await onAccountSuccessAsync(!result.IsPendingOnboarding && result.IsOnboardingComplete)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (PlatformNotSupportedException)
        {
            setErrorMessage("This sign in option is unavailable on this device.");
        }
        catch (InvalidOperationException ex) when (!string.IsNullOrWhiteSpace(ex.Message))
        {
            Trace.WriteLine($"[{callerName}] {providerName} sign in failed ({ex.GetType().Name}).");
            setErrorMessage(ex.Message);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{callerName}] {providerName} sign in failed ({ex.GetType().Name}).");
            setErrorMessage($"{providerName} sign in could not finish. Check your connection and try again.");
        }
    }
}
