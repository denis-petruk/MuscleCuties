using System.Diagnostics;
using System.ComponentModel;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

public partial class PasswordRecoveryPage : ContentPage
{
    private readonly PasswordRecoveryViewModel _viewModel;
    private bool _isSubscribed;

    public PasswordRecoveryPage(PasswordRecoveryViewModel viewModel)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = viewModel;
        this.BindWithTiming(viewModel, started);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_isSubscribed)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _isSubscribed = true;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PasswordRecoveryViewModel.Step))
            return;

        ResetPasswordMasks();

        if (_viewModel.Step == PasswordRecoveryStep.EnterCode)
            RecoveryIdentifierEntry.Unfocus();
        else if (_viewModel.Step == PasswordRecoveryStep.SetNewPassword)
            RecoveryOtpEntry.Unfocus();

        var step = _viewModel.Step;
        var nextEntry = step switch
        {
            PasswordRecoveryStep.EnterIdentifier => RecoveryIdentifierEntry,
            PasswordRecoveryStep.EnterCode => RecoveryOtpEntry,
            PasswordRecoveryStep.SetNewPassword => NewPasswordEntry,
            _ => null
        };
        if (nextEntry is null)
            return;

        // Wait for the step's visibility change to lay out, then start the new step at its
        // instructions and hand focus to its first field so the keyboard flow continues.
        Dispatcher.Dispatch(async () =>
        {
            if (Handler is null || _viewModel.Step != step)
                return;
            await RecoveryScroll.ScrollToAsync(0, 0, false);
            nextEntry.Focus();
        });
    }

    private void OnNewPasswordCompleted(object? sender, EventArgs e)
    {
        ConfirmPasswordEntry.Focus();
    }

    private void OnToggleNewPassword(object? sender, TappedEventArgs e)
    {
        if (_viewModel.IsBusy)
            return;

        NewPasswordEntry.IsPassword = !NewPasswordEntry.IsPassword;
        NewPasswordReveal.Text = UiTextCatalog.Get(
            NewPasswordEntry.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(NewPasswordReveal, NewPasswordReveal.Text);
    }

    private void OnToggleConfirmPassword(object? sender, TappedEventArgs e)
    {
        if (_viewModel.IsBusy)
            return;

        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordReveal.Text = UiTextCatalog.Get(
            ConfirmPasswordEntry.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(ConfirmPasswordReveal, ConfirmPasswordReveal.Text);
    }

    private void ResetPasswordMasks()
    {
        NewPasswordEntry.IsPassword = true;
        ConfirmPasswordEntry.IsPassword = true;
        var show = UiTextCatalog.Get("Auth_ShowPassword");
        NewPasswordReveal.Text = show;
        ConfirmPasswordReveal.Text = show;
        SemanticProperties.SetDescription(NewPasswordReveal, show);
        SemanticProperties.SetDescription(ConfirmPasswordReveal, show);
    }

    protected override void OnDisappearing()
    {
        ResetPasswordMasks();
        if (_isSubscribed)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _isSubscribed = false;
        }

        _viewModel.Dispose();
        base.OnDisappearing();
    }
}
