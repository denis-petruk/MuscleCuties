using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IAppleSignInService? _appleSignInService;
    private readonly Action _navigateToProfileSetup;
    private readonly Action? _navigateToDashboard;
    private readonly Action _navigateBack;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public AsyncRelayCommand RegisterCommand { get; }
    public AsyncRelayCommand SignInWithAppleCommand { get; }
    public RelayCommand GoBackCommand { get; }

    public RegisterViewModel(
        IAuthService authService,
        Action navigateToProfileSetup,
        Action navigateBack,
        IAppleSignInService? appleSignInService = null,
        Action? navigateToDashboard = null)
    {
        _authService = authService;
        _appleSignInService = appleSignInService;
        _navigateToProfileSetup = navigateToProfileSetup;
        _navigateToDashboard = navigateToDashboard;
        _navigateBack = navigateBack;
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        SignInWithAppleCommand = new AsyncRelayCommand(SignInWithAppleAsync);
        GoBackCommand = new RelayCommand(() => _navigateBack());
    }

    private async Task RegisterAsync()
    {
        AppDebugLog.Write("Register", "Register command started.");
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var email = Email.Trim();
            if (!AuthInputValidator.IsValidEmail(email))
            {
                ErrorMessage = "Enter a valid email address.";
                AppDebugLog.Write("Register", "Register blocked: invalid email.");
                return;
            }

            if (!AuthInputValidator.IsStrongPassword(Password))
            {
                ErrorMessage = AuthInputValidator.PasswordRequirementsMessage;
                AppDebugLog.Write("Register", "Register blocked: weak password.");
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match";
                AppDebugLog.Write("Register", "Register blocked: password confirmation mismatch.");
                return;
            }

            var user = await _authService.RegisterAsync(email, Password);
            if (user is null)
            {
                ErrorMessage = "Registration failed";
                AppDebugLog.Write("Register", "Register failed: auth service returned null user.");
                return;
            }

            AppDebugLog.Write("Register", $"Register succeeded userId={user.Id}. Navigating to ProfileSetupPage.");
            _navigateToProfileSetup();
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("Register", ex, "Register command failed");
            throw;
        }
        finally
        {
            IsBusy = false;
            AppDebugLog.Write("Register", "Register command finished.");
        }
    }

    private async Task SignInWithAppleAsync()
    {
        AppDebugLog.Write("Register", "Apple login command started from register page.");
        if (_appleSignInService is null)
        {
            ErrorMessage = "Apple sign in is not available in this build.";
            AppDebugLog.Write("Register", "Apple login unavailable: service is null.");
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var appleAccount = await _appleSignInService.SignInAsync();
            if (appleAccount is null)
            {
                AppDebugLog.Write("Register", "Apple login cancelled or returned no account.");
                return;
            }

            var user = await _authService.SignInWithAppleAsync(appleAccount);
            if (user is null)
            {
                ErrorMessage = "Apple sign in could not finish. Please try again.";
                AppDebugLog.Write("Register", "Apple login failed: auth service returned null user.");
                return;
            }

            if (user.IsOnboardingComplete && _navigateToDashboard is not null)
            {
                AppDebugLog.Write("Register", $"Apple login userId={user.Id} onboarding complete. Navigating to dashboard.");
                _navigateToDashboard();
                return;
            }

            AppDebugLog.Write("Register", $"Apple login userId={user.Id} onboarding incomplete. Navigating to profile setup.");
            _navigateToProfileSetup();
        }
        catch (OperationCanceledException)
        {
            AppDebugLog.Write("Register", "Apple login cancelled.");
        }
        catch (PlatformNotSupportedException ex)
        {
            ErrorMessage = ex.Message;
            AppDebugLog.Error("Register", ex, "Apple login platform is not supported");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Apple sign in could not finish. Please try again.";
            AppDebugLog.Error("Register", ex, "Apple login command failed");
        }
        finally
        {
            IsBusy = false;
            AppDebugLog.Write("Register", "Apple login command finished from register page.");
        }
    }
}
