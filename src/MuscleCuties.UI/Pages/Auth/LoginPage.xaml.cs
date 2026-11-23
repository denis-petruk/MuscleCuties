using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

public partial class LoginPage : ContentPage
{
    internal const string ExistingEmailContextKey = "login_existing_email";
    internal const string PasswordUpdatedEmailContextKey = "login_password_updated_email";
    private readonly INavigationContextService _navigationContext;

    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel vm, INavigationContextService navigationContext)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
        _navigationContext = navigationContext;
        this.BindWithTiming(vm, started);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Consumes one-time email handoffs from PasswordRecoveryPage or RegisterPage,
        // then delays focus by 75ms so the Shell transition completes before invoking the soft keyboard.
        if (_navigationContext.TryTake<string>(PasswordUpdatedEmailContextKey, out var recoveredEmail) &&
            !string.IsNullOrWhiteSpace(recoveredEmail))
        {
            ResetPasswordMask();
            _viewModel.PrepareForPasswordUpdated(recoveredEmail);
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(75), () => LoginPassword.Focus());
            return;
        }

        if (_navigationContext.TryTake<string>(ExistingEmailContextKey, out var email) &&
            !string.IsNullOrWhiteSpace(email))
        {
            ResetPasswordMask();
            _viewModel.PrepareForExistingAccount(email);
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(75), () => LoginPassword.Focus());
        }
    }

    protected override void OnDisappearing()
    {
        ResetPasswordMask();
        base.OnDisappearing();
    }

    private void OnEmailCompleted(object? sender, EventArgs e)
    {
        LoginPassword.Focus();
    }

    private void OnToggleLoginPassword(object? sender, TappedEventArgs e)
    {
        if (_viewModel.IsBusy)
            return;

        LoginPassword.IsPassword = !LoginPassword.IsPassword;
        LoginPasswordReveal.Text = UiTextCatalog.Get(
            LoginPassword.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(
            LoginPasswordReveal, LoginPasswordReveal.Text);
    }

    private void ResetPasswordMask()
    {
        LoginPassword.IsPassword = true;
        LoginPasswordReveal.Text = UiTextCatalog.Get("Auth_ShowPassword");
        SemanticProperties.SetDescription(LoginPasswordReveal, LoginPasswordReveal.Text);
    }
}
