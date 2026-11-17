using System.Diagnostics;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        this.BindWithTiming(vm, started);
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is RegisterViewModel viewModel)
            viewModel.CancelPendingRequest();
        base.OnDisappearing();
    }

    private void OnEmailCompleted(object? sender, EventArgs e)
    {
        RegisterUsername.Focus();
    }

    private void OnToggleRegisterPassword(object? sender, TappedEventArgs e)
    {
        RegisterPassword.IsPassword = !RegisterPassword.IsPassword;
        RegisterPasswordReveal.Text = UiTextCatalog.Get(
            RegisterPassword.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(
            RegisterPasswordReveal, RegisterPasswordReveal.Text);
    }

    private void OnToggleRegisterConfirmPassword(object? sender, TappedEventArgs e)
    {
        RegisterConfirmPassword.IsPassword = !RegisterConfirmPassword.IsPassword;
        RegisterConfirmPasswordReveal.Text = UiTextCatalog.Get(
            RegisterConfirmPassword.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(
            RegisterConfirmPasswordReveal, RegisterConfirmPasswordReveal.Text);
    }

    private void OnUsernameCompleted(object? sender, EventArgs e)
    {
        RegisterPassword.Focus();
    }

    private void OnPasswordCompleted(object? sender, EventArgs e)
    {
        RegisterConfirmPassword.Focus();
    }

    // The acknowledgment copy toggles the same bound checkbox, so the label is part of the
    // tap target without introducing a second source of consent state.
    private void OnPrivacyConsentLabelTapped(object? sender, TappedEventArgs e)
    {
        PrivacyConsentCheckBox.IsChecked = !PrivacyConsentCheckBox.IsChecked;
    }
}
