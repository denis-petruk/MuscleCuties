using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly Func<Task> _navigateToDashboardAsync;
    private readonly Func<Task> _navigateToProfileSetupAsync;
    private readonly Func<Task> _navigateToRegisterAsync;
    private readonly IPlatformSignInService? _platformSignInService;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _password = string.Empty;

    public LoginViewModel(
        IAuthService authService,
        Func<Task> navigateToDashboardAsync,
        Func<Task> navigateToProfileSetupAsync,
        Func<Task> navigateToRegisterAsync,
        IPlatformSignInService? platformSignInService = null)
    {
        _authService = authService;
        _platformSignInService = platformSignInService;
        _navigateToDashboardAsync = navigateToDashboardAsync;
        _navigateToProfileSetupAsync = navigateToProfileSetupAsync;
        _navigateToRegisterAsync = navigateToRegisterAsync;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        SignInWithPlatformCommand = new AsyncRelayCommand(SignInWithPlatformAsync);
        GoToRegisterCommand = new AsyncRelayCommand(_navigateToRegisterAsync);
    }

    public AsyncRelayCommand LoginCommand { get; }
    public AsyncRelayCommand SignInWithPlatformCommand { get; }
    public AsyncRelayCommand SignInWithAppleCommand => SignInWithPlatformCommand;
    public AsyncRelayCommand GoToRegisterCommand { get; }
    public string PlatformLoginButtonText => _platformSignInService?.LoginButtonText ?? "Continue with device account";

    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var user = await _authService.LoginAsync(Email, Password);
            if (user is null)
            {
                ErrorMessage = "Invalid email or password";
                return;
            }

            await NavigateAfterSignInAsync(user);
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

            await NavigateAfterSignInAsync(user);
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

    private Task NavigateAfterSignInAsync(User user)
    {
        return user.IsOnboardingComplete
            ? _navigateToDashboardAsync()
            : _navigateToProfileSetupAsync();
    }
}
