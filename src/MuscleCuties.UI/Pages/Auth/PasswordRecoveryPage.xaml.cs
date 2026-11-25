using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

/// <summary>
///     Password recovery page: request a code, verify it, then set a new password.
/// </summary>
public partial class PasswordRecoveryPage : ContentPage
{
    private readonly PasswordRecoveryViewModel _viewModel;
    private bool _isSubscribed;

    /// <summary>Initializes the page and its view model.</summary>
    public PasswordRecoveryPage(PasswordRecoveryViewModel viewModel)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = viewModel;
        this.BindWithTiming(viewModel, started);
    }

    /// <summary>Starts the view model's work when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_isSubscribed)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _isSubscribed = true;
        }
    }

    /// <summary>Follows the recovery step changes the view model reports.</summary>
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

    /// <summary>Moves to the confirmation field.</summary>
    private void OnNewPasswordCompleted(object? sender, EventArgs e)
    {
        ConfirmPasswordEntry.Focus();
    }

    /// <summary>Reveals or re-hides the new password and updates the label.</summary>
    private void OnToggleNewPassword(object? sender, TappedEventArgs e)
    {
        if (_viewModel.IsBusy)
            return;

        NewPasswordEntry.IsPassword = !NewPasswordEntry.IsPassword;
        NewPasswordReveal.Text = UiTextCatalog.Get(
            NewPasswordEntry.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(NewPasswordReveal, NewPasswordReveal.Text);
    }

    /// <summary>Reveals or re-hides the confirmation field and updates the label.</summary>
    private void OnToggleConfirmPassword(object? sender, TappedEventArgs e)
    {
        if (_viewModel.IsBusy)
            return;

        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordReveal.Text = UiTextCatalog.Get(
            ConfirmPasswordEntry.IsPassword ? "Auth_ShowPassword" : "Auth_HidePassword");
        SemanticProperties.SetDescription(ConfirmPasswordReveal, ConfirmPasswordReveal.Text);
    }

    /// <summary>Masks both password fields again when the page is left, so neither stays readable.</summary>
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

    /// <summary>Pauses the view model when the page goes away.</summary>
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
