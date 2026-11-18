using System.Diagnostics;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

public partial class LoginPage : ContentPage
{
    internal const string ExistingEmailContextKey = "login_existing_email";
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

        // Consumes the one-time email handoff from RegisterPage when an existing account is detected,
        // then delays focus by 75ms so the Shell transition completes before invoking the soft keyboard.
        if (_navigationContext.TryTake<string>(ExistingEmailContextKey, out var email) &&
            !string.IsNullOrWhiteSpace(email))
        {
            _viewModel.PrepareForExistingAccount(email);
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(75), () => LoginPassword.Focus());
        }
    }

    private void OnEmailCompleted(object? sender, EventArgs e)
    {
        LoginPassword.Focus();
    }

    private void OnToggleLoginPassword(object? sender, TappedEventArgs e)
    {
        LoginPassword.IsPassword = !LoginPassword.IsPassword;
        LoginPasswordReveal.Text = UiTextCatalog.Get(
            LoginPassword.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(
            LoginPasswordReveal, LoginPasswordReveal.Text);
    }
}
