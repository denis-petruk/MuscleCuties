using Microsoft.Maui.Accessibility;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

/// <summary>
///     Sign-in state for the login page: credentials, validation, and the route taken after a successful sign-in.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAccountLoginScheduler _loginScheduler;
    private readonly Func<Task> _navigateToDashboardAsync;
    private readonly Func<Task> _navigateToProfileSetupAsync;
    private readonly Func<Task>? _navigateToRecoveryAsync;
    private readonly IPlatformAccountSignInAdapter _platformSignIn;
    private readonly IAccountSignInUseCase _signIn;
    private readonly IPhotoCacheAccountLifecycle? _photoCacheAccountLifecycle;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _infoMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _password = string.Empty;

    /// <summary>Takes the sign-in use cases and the navigation callbacks.</summary>
    public LoginViewModel(
        IAccountSignInUseCase signIn,
        IAccountLoginScheduler loginScheduler,
        Func<Task> navigateToDashboardAsync,
        Func<Task> navigateToProfileSetupAsync,
        Func<Task> navigateToRegisterAsync,
        IPlatformAccountSignInAdapter platformSignInService,
        Func<Task>? navigateToRecoveryAsync = null,
        IPhotoCacheAccountLifecycle? photoCacheAccountLifecycle = null)
    {
        _signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        _loginScheduler = loginScheduler ?? throw new ArgumentNullException(nameof(loginScheduler));
        _platformSignIn = platformSignInService ?? throw new ArgumentNullException(nameof(platformSignInService));
        _navigateToDashboardAsync = navigateToDashboardAsync;
        _navigateToProfileSetupAsync = navigateToProfileSetupAsync;
        _navigateToRecoveryAsync = navigateToRecoveryAsync;
        _photoCacheAccountLifecycle = photoCacheAccountLifecycle;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
        GoToRegisterCommand = new AsyncRelayCommand(navigateToRegisterAsync, () => !IsBusy);
        GoToRecoveryCommand = new AsyncRelayCommand(HandleRecoveryNavigationAsync, () => !IsBusy);
        SignInWithPlatformCommand = new AsyncRelayCommand(SignInWithPlatformAsync, () => !IsBusy);
    }

    /// <summary>True when the platform can sign in.</summary>
    public bool IsPlatformSignInAvailable => _platformSignIn.IsAvailable;
    /// <summary>True when Apple sign-in is available on this device.</summary>
    public bool IsAppleSignInAvailable => IsPlatformSignInAvailable;

    /// <summary>Signs in with the entered credentials.</summary>
    public AsyncRelayCommand LoginCommand { get; }
    /// <summary>Opens the registration page.</summary>
    public AsyncRelayCommand GoToRegisterCommand { get; }
    /// <summary>Opens the password recovery page.</summary>
    public AsyncRelayCommand GoToRecoveryCommand { get; }
    /// <summary>Signs in through the platform account.</summary>
    public AsyncRelayCommand SignInWithPlatformCommand { get; }
    /// <summary>Signs in with Apple.</summary>
    public AsyncRelayCommand SignInWithAppleCommand => SignInWithPlatformCommand;

    /// <summary>Clears the error when the email changes.</summary>
    partial void OnEmailChanged(string value)
    {
        InfoMessage = string.Empty;
        ErrorMessage = string.Empty;
    }

    /// <summary>Clears the error when the password changes.</summary>
    partial void OnPasswordChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        LoginCommand.NotifyCanExecuteChanged();
        GoToRegisterCommand.NotifyCanExecuteChanged();
        GoToRecoveryCommand.NotifyCanExecuteChanged();
        SignInWithPlatformCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Points the form at an account that already exists — clearing the password and any error — and explains in</summary>
    /// the info line why the user was sent back here.</summary>
    public void PrepareForExistingAccount(string email)
    {
        Email = email;
        Password = string.Empty;
        ErrorMessage = string.Empty;
        InfoMessage = "Ui_Auth_Info_ExistingAccount";
    }

    /// <summary>Same hand-off after a password reset, so the user signs in with the new password rather than a stale</summary>
    /// one.</summary>
    public void PrepareForPasswordUpdated(string email)
    {
        Email = email;
        Password = string.Empty;
        ErrorMessage = string.Empty;
        InfoMessage = "Ui_Auth_Info_PasswordUpdated";
    }

    /// <summary>Opens password recovery, or says it is unavailable when the page was given no route for it, so the action</summary>
    /// never silently does nothing.</summary>
    private async Task HandleRecoveryNavigationAsync()
    {
        if (_navigateToRecoveryAsync is not null)
            await _navigateToRecoveryAsync();
        else
            ErrorMessage = "Ui_Auth_Error_RecoveryUnavailable";
    }

    /// <summary>Signs in once, refusing a second attempt while one is running, and turns the answer's outcome into a</summary>
    /// navigation or an error on the page.</summary>
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        var missingEmail = string.IsNullOrWhiteSpace(Email);
        var missingPassword = string.IsNullOrWhiteSpace(Password);
        if (missingEmail || missingPassword)
        {
            ErrorMessage = missingEmail && missingPassword
                ? "Ui_Auth_Error_IdentifierAndPasswordRequired"
                : missingEmail
                    ? "Ui_Auth_Error_IdentifierRequired"
                    : "Ui_Auth_Error_PasswordRequired";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _loginScheduler.RunAsync(() => _signIn.SignInWithPasswordAsync(
                new PasswordSignInRequest(Email, Password)));
            if (!result.Succeeded)
            {
                ErrorMessage = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? result.ErrorMessage
                    : "Ui_Auth_Error_InvalidCredentials";
                return;
            }

            await ActivateAndNavigateAsync(result.IsOnboardingComplete);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LoginViewModel] Sign in failed ({ex.GetType().Name}).");
            ErrorMessage = "Ui_Auth_Error_SignIn";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The platform sign-in path, which can end in a new account that still has to finish onboarding.</summary>
    private async Task SignInWithPlatformAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await PlatformSignInCoordinator.ExecuteAsync(
                _platformSignIn,
                _signIn,
                ActivateAndNavigateAsync,
                msg => ErrorMessage = msg,
                nameof(LoginViewModel));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Activates the signed-in account's photo cache before navigating, so a previous account's cached images can</summary>
    /// never be shown to this one.</summary>
    private async Task ActivateAndNavigateAsync(bool isOnboardingComplete)
    {
        if (_photoCacheAccountLifecycle is not null)
            await _photoCacheAccountLifecycle.ActivateCurrentAccountAsync();
        if (isOnboardingComplete)
            await _navigateToDashboardAsync();
        else
            await _navigateToProfileSetupAsync();
    }
}
