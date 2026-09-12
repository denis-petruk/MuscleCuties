using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task>? _navigateToDashboardAsync;
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
        Func<Task>? navigateToDashboardAsync = null)
    {
        _authService = authService;
        _platformSignInService = platformSignInService;
        _navigateToProfileSetupAsync = navigateToProfileSetupAsync;
        _navigateToDashboardAsync = navigateToDashboardAsync;
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

            var user = await _authService.RegisterAsync(email, Password);
            if (user is null)
            {
                ErrorMessage = "Registration failed";
                return;
            }

            await _navigateToProfileSetupAsync();
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
        catch (PlatformNotSupportedException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildPlatformSignInErrorMessage(providerName, ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildPlatformSignInErrorMessage(string providerName, Exception ex)
    {
        if (ex is InvalidOperationException &&
            !string.IsNullOrWhiteSpace(ex.Message))
            return ex.Message;

        return $"{providerName} sign in could not finish. Please try again.";
    }
}
