using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App;

public partial class App : Application
{
#if DEBUG
    // Flip this locally when you intentionally want a fresh debug database on startup.
    private static readonly bool ResetDatabaseOnDebugStart = false;
#endif

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

        using var scope = _services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDatabase>();
#if DEBUG
        if (ResetDatabaseOnDebugStart)
            await database.ResetAndSeedDebugDatabaseAsync();
        else
            await database.InitializeAsync();
#else
        await database.InitializeAsync();
#endif

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var isLoggedIn = await authService.IsLoggedInAsync();
        if (isLoggedIn)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await Shell.Current.GoToAsync("//DashboardPage", false);

            var notificationService = scope.ServiceProvider.GetRequiredService<ICyclePhaseNotificationService>();
            await notificationService.NotifyIfPhaseChangedAsync(userId);
            return;
        }

        await Shell.Current.GoToAsync("//LoginPage", false);
    }
}
