using System.Diagnostics;
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

    private void OnUsernameCompleted(object? sender, EventArgs e)
    {
        RegisterPassword.Focus();
    }

    private void OnPasswordCompleted(object? sender, EventArgs e)
    {
        RegisterConfirmPassword.Focus();
    }
}
