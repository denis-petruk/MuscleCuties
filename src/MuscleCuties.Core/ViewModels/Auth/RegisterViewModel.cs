using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task>? _navigateToDashboardAsync;
    private readonly Func<string, Task>? _navigateToExistingLoginAsync;
    private readonly Func<Task> _navigateToProfileSetupAsync;
    private readonly IPlatformSignInService? _platformSignInService;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _password = string.Empty;

    public RegisterViewModel(
        IAuthService authService,
        Func<Task> navigateToProfileSetupAsync,
        Func<Task> navigateBackAsync,
        IPlatformSignInService? platformSignInService = null,
        Func<Task>? navigateToDashboardAsync = null,
        Func<string, Task>? navigateToExistingLoginAsync = null)
    {
        _authService = authService;
        _platformSignInService = platformSignInService;
        _navigateToProfileSetupAsync = navigateToProfileSetupAsync;
        _navigateToDashboardAsync = navigateToDashboardAsync;
        _navigateToExistingLoginAsync = navigateToExistingLoginAsync;
        _navigateBackAsync = navigateBackAsync;
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        SignInWithPlatformCommand = new AsyncRelayCommand(SignInWithPlatformAsync);
        GoBackCommand = new AsyncRelayCommand(_navigateBackAsync);
    }

    public AsyncRelayCommand RegisterCommand { get; }
    public AsyncRelayCommand SignInWithPlatformCommand { get; }
    public AsyncRelayCommand SignInWithAppleCommand => SignInWithPlatformCommand;
    public AsyncRelayCommand GoBackCommand { get; }

    public string PlatformRegisterButtonText =>
        _platformSignInService?.RegisterButtonText ?? "Continue with device account";

    private async Task RegisterAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var email = Email.Trim();
            if (!AuthInputValidator.IsValidEmail(email))
            {
                ErrorMessage = "Enter a valid email address.";
                return;
            }

            if (await DataLoadScheduler.RunAsync(() => _authService.EmailExistsAsync(email)))
            {
                await RedirectExistingAccountAsync(email);
                return;
            }

            if (!AuthInputValidator.IsStrongPassword(Password))
            {
                ErrorMessage = AuthInputValidator.PasswordRequirementsMessage;
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match";
                return;
            }

            var user = await DataLoadScheduler.RunAsync(() => _authService.RegisterAsync(email, Password));
            if (user is null)
            {
                await RedirectExistingAccountAsync(email);
                return;
            }

            await _navigateToProfileSetupAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "We couldn't finish creating your account securely. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RedirectExistingAccountAsync(string email)
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;

        if (_navigateToExistingLoginAsync is null)
        {
            ErrorMessage = "You already have an account. Log in with your password.";
            return;
        }

        await _navigateToExistingLoginAsync(email.Trim().ToLowerInvariant());
    }

    private async Task SignInWithPlatformAsync()
    {
        if (IsBusy)
            return;

        var providerName = _platformSignInService?.ProviderName ?? "device account";
        if (_platformSignInService is null)
        {
            ErrorMessage = "Device account sign in is not available in this build.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var platformAccount = await _platformSignInService.SignInWithProviderAsync();
            if (platformAccount is null)
                return;

            var user = await _authService.SignInWithExternalProviderAsync(platformAccount);
            if (user is null)
            {
                ErrorMessage = $"{providerName} sign in could not finish. Please try again.";
                return;
            }

            if (user.IsOnboardingComplete && _navigateToDashboardAsync is not null)
            {
                await _navigateToDashboardAsync();
                return;
            }

            await _navigateToProfileSetupAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (PlatformNotSupportedException)
        {
            ErrorMessage = "This sign in option is unavailable on this device.";
        }
        catch (Exception)
        {
            ErrorMessage = $"{providerName} sign in could not finish. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
