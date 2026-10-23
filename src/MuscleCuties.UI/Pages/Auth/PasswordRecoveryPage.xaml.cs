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
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
    }
}
