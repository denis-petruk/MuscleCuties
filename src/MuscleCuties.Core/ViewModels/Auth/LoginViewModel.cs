using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IAppleSignInService? _appleSignInService;
    private readonly Action _navigateToDashboard;
    private readonly Action _navigateToProfileSetup;
    private readonly Action _navigateToRegister;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public AsyncRelayCommand LoginCommand { get; }
    public AsyncRelayCommand SignInWithAppleCommand { get; }
    public RelayCommand GoToRegisterCommand { get; }

    public LoginViewModel(
        IAuthService authService,
        Action navigateToDashboard,
        Action navigateToProfileSetup,
        Action navigateToRegister,
        IAppleSignInService? appleSignInService = null)
    {
        _authService = authService;
        _appleSignInService = appleSignInService;
        _navigateToDashboard = navigateToDashboard;
        _navigateToProfileSetup = navigateToProfileSetup;
        _navigateToRegister = navigateToRegister;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        SignInWithAppleCommand = new AsyncRelayCommand(SignInWithAppleAsync);
        GoToRegisterCommand = new RelayCommand(() => _navigateToRegister());
    }

    private async Task LoginAsync()
    {
        AppDebugLog.Write("Login", "Login command started.");
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var user = await _authService.LoginAsync(Email, Password);
            if (user is null)
            {
                ErrorMessage = "Invalid email or password";
                AppDebugLog.Write("Login", "Login failed: invalid credentials.");
                return;
            }

            AppDebugLog.Write("Login", $"Login succeeded for userId={user.Id}, onboardingComplete={user.IsOnboardingComplete}.");
            await NavigateAfterSignInAsync(user);
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("Login", ex, "Login command failed");
            throw;
        }
        finally
        {
            IsBusy = false;
            AppDebugLog.Write("Login", "Login command finished.");
        }
    }

    private async Task SignInWithAppleAsync()
    {
        AppDebugLog.Write("Login", "Apple login command started.");
        if (_appleSignInService is null)
        {
            ErrorMessage = "Apple sign in is not available in this build.";
            AppDebugLog.Write("Login", "Apple login unavailable: service is null.");
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var appleAccount = await _appleSignInService.SignInAsync();
            if (appleAccount is null)
            {
                AppDebugLog.Write("Login", "Apple login cancelled or returned no account.");
                return;
            }

            var user = await _authService.SignInWithAppleAsync(appleAccount);
            if (user is null)
            {
                ErrorMessage = "Apple sign in could not finish. Please try again.";
                AppDebugLog.Write("Login", "Apple login failed: auth service returned null user.");
                return;
            }

            AppDebugLog.Write("Login", $"Apple login succeeded for userId={user.Id}, onboardingComplete={user.IsOnboardingComplete}.");
            await NavigateAfterSignInAsync(user);
        }
        catch (OperationCanceledException)
        {
            AppDebugLog.Write("Login", "Apple login cancelled.");
        }
        catch (PlatformNotSupportedException ex)
        {
            ErrorMessage = ex.Message;
            AppDebugLog.Error("Login", ex, "Apple login platform is not supported");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Apple sign in could not finish. Please try again.";
            AppDebugLog.Error("Login", ex, "Apple login command failed");
        }
        finally
        {
            IsBusy = false;
            AppDebugLog.Write("Login", "Apple login command finished.");
        }
    }

    private Task NavigateAfterSignInAsync(User user)
    {
        if (!user.IsOnboardingComplete)
        {
            AppDebugLog.Write("Login", $"NavigateAfterSignIn: userId={user.Id} -> ProfileSetupPage.");
            _navigateToProfileSetup();
            return Task.CompletedTask;
        }

        AppDebugLog.Write("Login", $"NavigateAfterSignIn: userId={user.Id} -> DashboardPage.");
        _navigateToDashboard();
        return Task.CompletedTask;
    }
}
