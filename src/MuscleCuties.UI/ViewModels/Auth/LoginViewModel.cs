using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.ViewModels.Auth;

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
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        GoToRegisterCommand = new AsyncRelayCommand(navigateToRegisterAsync);
        GoToRecoveryCommand = new AsyncRelayCommand(HandleRecoveryNavigationAsync);
        SignInWithPlatformCommand = new AsyncRelayCommand(SignInWithPlatformAsync);
    }

    public bool IsPlatformSignInAvailable => _platformSignIn.IsAvailable;
    public bool IsAppleSignInAvailable => IsPlatformSignInAvailable;
    public string PlatformLoginButtonText => _platformSignIn.LoginButtonText;

    public AsyncRelayCommand LoginCommand { get; }
    public AsyncRelayCommand GoToRegisterCommand { get; }
    public AsyncRelayCommand GoToRecoveryCommand { get; }
    public AsyncRelayCommand SignInWithPlatformCommand { get; }
    public AsyncRelayCommand SignInWithAppleCommand => SignInWithPlatformCommand;

    partial void OnEmailChanged(string value)
    {
        InfoMessage = string.Empty;
    }

    public void PrepareForExistingAccount(string email)
    {
        Email = email;
        Password = string.Empty;
        ErrorMessage = string.Empty;
        InfoMessage = "You're already with us. Enter your password to log in.";
    }

    private async Task HandleRecoveryNavigationAsync()
    {
        if (_navigateToRecoveryAsync is not null)
            await _navigateToRecoveryAsync();
        else
            ErrorMessage = "Password recovery is currently unavailable.";
    }

    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _loginScheduler.RunAsync(() => _signIn.SignInWithPasswordAsync(
                new PasswordSignInRequest(Email, Password)));
            if (!result.Succeeded)
            {
                ErrorMessage = "Invalid email or password";
                return;
            }

            await NavigateForAccountAsync(result.IsOnboardingComplete);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LoginViewModel] Sign in failed ({ex.GetType().Name}).");
            ErrorMessage = "We couldn't sign you in securely. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
                NavigateForAccountAsync,
                msg => ErrorMessage = msg,
                nameof(LoginViewModel));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task NavigateForAccountAsync(bool isOnboardingComplete)
    {
        return ActivateAndNavigateAsync(isOnboardingComplete);
    }

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
