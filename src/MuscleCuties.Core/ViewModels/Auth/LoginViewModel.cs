using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IAppleSignInService? _appleSignInService;
    private readonly IQuizService _quizService;
    private readonly Action _navigateToDashboard;
    private readonly Action _navigateToQuiz;
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
        IQuizService quizService,
        Action navigateToDashboard,
        Action navigateToQuiz,
        Action navigateToRegister,
        IAppleSignInService? appleSignInService = null)
    {
        _authService = authService;
        _appleSignInService = appleSignInService;
        _quizService = quizService;
        _navigateToDashboard = navigateToDashboard;
        _navigateToQuiz = navigateToQuiz;
        _navigateToRegister = navigateToRegister;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        SignInWithAppleCommand = new AsyncRelayCommand(SignInWithAppleAsync);
        GoToRegisterCommand = new RelayCommand(() => _navigateToRegister());
    }

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

            await NavigateAfterSignInAsync(user);
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

    private async Task NavigateAfterSignInAsync(User user)
    {
        var onboardingComplete = await _quizService.IsOnboardingCompleteAsync(user.Id);
        if (!onboardingComplete)
        {
            _navigateToQuiz();
            return;
        }

        _navigateToDashboard();
    }
}
