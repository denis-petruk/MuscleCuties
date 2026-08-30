using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App;

public partial class App : Application
{
#if DEBUG
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

        AppDebugLog.Write("Startup", "OnStart begin.");
        try
        {
            using var scope = _services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDatabase>();
#if DEBUG
            AppDebugLog.Write("Startup", $"Database reset on debug start: {ResetDatabaseOnDebugStart}.");
            if (ResetDatabaseOnDebugStart)
            {
                AppDebugLog.Write("Startup", "ResetAndSeedDebugDatabaseAsync start.");
                await database.ResetAndSeedDebugDatabaseAsync();
                AppDebugLog.Write("Startup", "ResetAndSeedDebugDatabaseAsync complete.");
            }
            else
            {
                AppDebugLog.Write("Startup", "InitializeAsync start.");
                await database.InitializeAsync();
                AppDebugLog.Write("Startup", "InitializeAsync complete.");
            }
#else
            await database.InitializeAsync();
#endif

            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var isLoggedIn = await authService.IsLoggedInAsync();
            AppDebugLog.Write("Startup", $"IsLoggedInAsync returned {isLoggedIn}.");
            if (isLoggedIn)
            {
                var userId = await authService.GetCurrentUserIdAsync();
                AppDebugLog.Write("Startup", $"Current user id: {userId}.");
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await userRepository.GetByIdAsync(userId);
                if (user is null)
                {
                    AppDebugLog.Write("Startup", "User token exists but user row is missing. Logging out.");
                    await authService.LogoutAsync();
                    await Shell.Current.GoToAsync("//LoginPage", false);
                    AppDebugLog.Write("Startup", "Navigated to LoginPage after missing user.");
                    return;
                }

                AppDebugLog.Write("Startup", $"User loaded. OnboardingComplete={user.IsOnboardingComplete}.");
                if (!user.IsOnboardingComplete)
                {
                    await Shell.Current.GoToAsync(nameof(ProfileSetupPage), false);
                    AppDebugLog.Write("Startup", "Navigated to ProfileSetupPage.");
                    return;
                }

                await Shell.Current.GoToAsync("//DashboardPage", false);
                AppDebugLog.Write("Startup", "Navigated to DashboardPage.");

                var notificationService = scope.ServiceProvider.GetRequiredService<ICyclePhaseNotificationService>();
                await notificationService.NotifyIfPhaseChangedAsync(userId);
                AppDebugLog.Write("Startup", "Cycle phase notification check complete.");
                return;
            }

            await Shell.Current.GoToAsync("//LoginPage", false);
            AppDebugLog.Write("Startup", "Navigated to LoginPage.");
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("Startup", ex, "OnStart failed");
            throw;
        }
    }
}
