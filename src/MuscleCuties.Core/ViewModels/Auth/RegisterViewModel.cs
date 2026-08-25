using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.ViewModels.Auth;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly Action _navigateToQuiz;
    private readonly Action _navigateBack;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public AsyncRelayCommand RegisterCommand { get; }
    public RelayCommand GoBackCommand { get; }

    public RegisterViewModel(IAuthService authService, Action navigateToQuiz, Action navigateBack)
    {
        _authService = authService;
        _navigateToQuiz = navigateToQuiz;
        _navigateBack = navigateBack;
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        GoBackCommand = new RelayCommand(() => _navigateBack());
    }

    private async Task RegisterAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match";
                return;
            }

            var user = await _authService.RegisterAsync(Email, Password);
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
}
