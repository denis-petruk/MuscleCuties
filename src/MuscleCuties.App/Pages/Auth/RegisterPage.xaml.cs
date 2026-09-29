using System.Diagnostics;
using MuscleCuties.Core.ViewModels.Auth;

namespace MuscleCuties.App.Pages.Auth;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        this.BindWithTiming(vm, started);
    }

    private void OnEmailCompleted(object? sender, EventArgs e)
    {
        RegisterPassword.Focus();
    }

    private void OnPasswordCompleted(object? sender, EventArgs e)
    {
        RegisterConfirmPassword.Focus();
    }
}
