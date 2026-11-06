using System.Diagnostics;
using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

public partial class PasswordRecoveryPage : ContentPage
{
    private readonly PasswordRecoveryViewModel _viewModel;

    public PasswordRecoveryPage(PasswordRecoveryViewModel viewModel)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = viewModel;
        this.BindWithTiming(viewModel, started);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PasswordRecoveryViewModel.Step))
            return;

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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
    }
}
