using System.Diagnostics;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.Services.Auth;

public interface IPlatformAccountSignInAdapter
{
    bool IsAvailable { get; }
    string ProviderName { get; }
    string LoginButtonText { get; }
    string RegisterButtonText { get; }
    Task<PlatformSignInRequest?> SignInAsync();
}

public sealed class PlatformAccountSignInAdapter(IPlatformSignInService? platformSignInService)
    : IPlatformAccountSignInAdapter
{
    public bool IsAvailable => platformSignInService is not null;
    public string ProviderName => platformSignInService?.ProviderName ?? "Apple";
    public string LoginButtonText => platformSignInService?.LoginButtonText ?? "Continue with device account";
    public string RegisterButtonText => platformSignInService?.RegisterButtonText ?? "Continue with device account";

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

public static class PlatformSignInCoordinator
{
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
                setErrorMessage(result.ErrorMessage ?? $"{providerName} sign in could not finish. Please try again.");
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
        catch (Exception ex)
        {
            Trace.WriteLine($"[{callerName}] {providerName} sign in failed ({ex.GetType().Name}).");
            setErrorMessage($"{providerName} sign in could not finish. Please try again.");
        }
    }
}
