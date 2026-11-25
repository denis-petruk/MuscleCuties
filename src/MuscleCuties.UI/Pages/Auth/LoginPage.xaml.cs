using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

/// <summary>
///     Sign-in page: email and password, Apple sign-in, and the links to registration and password recovery.
/// </summary>
public partial class LoginPage : ContentPage
{
    internal const string ExistingEmailContextKey = "login_existing_email";
    internal const string PasswordUpdatedEmailContextKey = "login_password_updated_email";
    private readonly INavigationContextService _navigationContext;

    private readonly LoginViewModel _viewModel;

    /// <summary>Initializes the page with its view model and navigation context.</summary>
    public LoginPage(LoginViewModel vm, INavigationContextService navigationContext)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
        _navigationContext = navigationContext;
        this.BindWithTiming(vm, started);
    }

    /// <summary>Starts the view model's work when the page appears.</summary>
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

    /// <summary>Pauses the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        ResetPasswordMask();
        base.OnDisappearing();
    }

    /// <summary>Moves to the password field once the address is entered, so the keyboard does not have to be</summary>
    /// dismissed between fields.</summary>
    private void OnEmailCompleted(object? sender, EventArgs e)
    {
        LoginPassword.Focus();
    }

    /// <summary>Reveals or re-hides the password and keeps the reveal label and its accessibility description in</summary>
    /// step. Ignored while a request is running, so the form cannot change mid-submit.</summary>
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

    /// <summary>Returns the field to its masked state and its label to "show", so a returning visitor never finds a</summary>
    /// password on screen.</summary>
    private void ResetPasswordMask()
    {
        LoginPassword.IsPassword = true;
        LoginPasswordReveal.Text = UiTextCatalog.Get("Auth_ShowPassword");
        SemanticProperties.SetDescription(LoginPasswordReveal, LoginPasswordReveal.Text);
    }
}
