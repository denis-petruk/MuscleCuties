using MuscleCuties.Core.Services;

namespace MuscleCuties.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_services.GetRequiredService<AppShell>());
    }

    protected override async void OnStart()
    {
        base.OnStart();
        var authService = _services.GetRequiredService<IAuthService>();
        var isLoggedIn = await authService.IsLoggedInAsync();
        if (isLoggedIn)
            await Shell.Current.GoToAsync("//DashboardPage");
    }
}
