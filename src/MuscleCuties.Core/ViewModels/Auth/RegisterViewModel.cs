using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IAppleSignInService? _appleSignInService;
    private readonly Action _navigateToQuiz;
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
        Action navigateToQuiz,
        Action navigateBack,
        IAppleSignInService? appleSignInService = null,
        Action? navigateToDashboard = null)
    {
        _authService = authService;
        _appleSignInService = appleSignInService;
        _navigateToQuiz = navigateToQuiz;
        _navigateToDashboard = navigateToDashboard;
        _navigateBack = navigateBack;
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        SignInWithAppleCommand = new AsyncRelayCommand(SignInWithAppleAsync);
        GoBackCommand = new RelayCommand(() => _navigateBack());
    }

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

            _navigateToQuiz();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignInWithAppleAsync()
    {
        if (_appleSignInService is null)
        {
            ErrorMessage = "Apple sign in is not available in this build.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var appleAccount = await _appleSignInService.SignInAsync();
            if (appleAccount is null)
                return;

            var user = await _authService.SignInWithAppleAsync(appleAccount);
            if (user is null)
            {
                ErrorMessage = "Apple sign in could not finish. Please try again.";
                return;
            }

            if (user.IsOnboardingComplete && _navigateToDashboard is not null)
            {
                _navigateToDashboard();
                return;
            }

            _navigateToQuiz();
        }
        catch (OperationCanceledException)
        {
        }
        catch (PlatformNotSupportedException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch
        {
            ErrorMessage = "Apple sign in could not finish. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
