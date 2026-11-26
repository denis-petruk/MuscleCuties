using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

/// <summary>
///     Account creation page: email, username, password, and the privacy policy consent.
/// </summary>
public partial class RegisterPage : ContentPage
{
    /// <summary>Initializes the page and its view model.</summary>
    public RegisterPage(RegisterViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        this.BindWithTiming(vm, started);
    }

    /// <summary>Pauses the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        ResetPasswordMasks();
        if (BindingContext is RegisterViewModel viewModel)
            viewModel.CancelPendingRequest();
        base.OnDisappearing();
    }

    /// <summary>Moves to the username field.</summary>
    private void OnRegisterEmailCompleted(object? sender, EventArgs e)
    {
        RegisterUsername.Focus();
    }

    /// <summary>Reveals or re-hides the password, ignored while the form is submitting.</summary>
    private void OnToggleRegisterPassword(object? sender, TappedEventArgs e)
    {
        if (BindingContext is RegisterViewModel { IsBusy: true })
            return;

        RegisterPassword.IsPassword = !RegisterPassword.IsPassword;
        RegisterPasswordReveal.Text = UiTextCatalog.Get(
            RegisterPassword.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(
            RegisterPasswordReveal, RegisterPasswordReveal.Text);
    }

    /// <summary>Reveals or re-hides the confirmation, ignored while the form is submitting.</summary>
    private void OnToggleRegisterConfirmPassword(object? sender, TappedEventArgs e)
    {
        if (BindingContext is RegisterViewModel { IsBusy: true })
            return;

        RegisterConfirmPassword.IsPassword = !RegisterConfirmPassword.IsPassword;
        RegisterConfirmPasswordReveal.Text = UiTextCatalog.Get(
            RegisterConfirmPassword.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(
            RegisterConfirmPasswordReveal, RegisterConfirmPasswordReveal.Text);
    }

    /// <summary>Moves to the password field.</summary>
    private void OnUsernameCompleted(object? sender, EventArgs e)
    {
        RegisterPassword.Focus();
    }

    /// <summary>Moves to the confirmation field.</summary>
    private void OnPasswordCompleted(object? sender, EventArgs e)
    {
        RegisterConfirmPassword.Focus();
    }

    // The acknowledgment copy toggles the same bound checkbox, so the label is part of the
    // tap target without introducing a second source of consent state.
    /// <summary>Toggles the consent checkbox from its label, which enlarges the tap target without a second source of</summary>
    /// consent state.</summary>
    private void OnPrivacyConsentLabelTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is RegisterViewModel { IsBusy: true })
            return;

        PrivacyConsentCheckBox.IsChecked = !PrivacyConsentCheckBox.IsChecked;
    }

    /// <summary>Masks both password fields when the page is left.</summary>
    private void ResetPasswordMasks()
    {
        RegisterPassword.IsPassword = true;
        RegisterConfirmPassword.IsPassword = true;
        var show = UiTextCatalog.Get("Auth_ShowPassword");
        RegisterPasswordReveal.Text = show;
        RegisterConfirmPasswordReveal.Text = show;
        SemanticProperties.SetDescription(RegisterPasswordReveal, show);
        SemanticProperties.SetDescription(RegisterConfirmPasswordReveal, show);
    }
}
