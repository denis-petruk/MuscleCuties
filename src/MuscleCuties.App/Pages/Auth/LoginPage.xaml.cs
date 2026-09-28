using System.Diagnostics;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.App.Services.Navigation;

namespace MuscleCuties.App.Pages.Auth;

public partial class LoginPage : ContentPage
{
    internal const string ExistingEmailContextKey = "login_existing_email";

    private readonly LoginViewModel _viewModel;
    private readonly INavigationContextService _navigationContext;

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

        if (!_navigationContext.TryTake<string>(ExistingEmailContextKey, out var email) ||
            string.IsNullOrWhiteSpace(email))
            return;

        _viewModel.PrepareForExistingAccount(email);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(75), () => LoginPassword.Focus());
    }
}
