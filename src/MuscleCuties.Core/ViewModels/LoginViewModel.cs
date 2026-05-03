using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IQuizService _quizService;
    private readonly Action _navigateToDashboard;
    private readonly Action _navigateToQuiz;
    private readonly Action _navigateToRegister;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public AsyncRelayCommand LoginCommand { get; }
    public RelayCommand GoToRegisterCommand { get; }

    public LoginViewModel(
        IAuthService authService,
        IQuizService quizService,
        Action navigateToDashboard,
        Action navigateToQuiz,
        Action navigateToRegister)
    {
        _authService = authService;
        _quizService = quizService;
        _navigateToDashboard = navigateToDashboard;
        _navigateToQuiz = navigateToQuiz;
        _navigateToRegister = navigateToRegister;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        GoToRegisterCommand = new RelayCommand(() => _navigateToRegister());
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required";
            return;
        }

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

            var onboardingComplete = await _quizService.IsOnboardingCompleteAsync(user.Id);
            if (onboardingComplete)
                _navigateToDashboard();
            else
                _navigateToQuiz();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
