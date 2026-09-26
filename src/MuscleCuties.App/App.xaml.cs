using System.Diagnostics;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Services.Security;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private int _startupStarted;
    private Task? _startupTask;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        SyncActivityTheme();
        RequestedThemeChanged += (_, e) => SyncActivityTheme(e.RequestedTheme);
    }

    private static void SyncActivityTheme(AppTheme? theme = null)
    {
        var resolved = theme ?? Current?.RequestedTheme ?? AppTheme.Unspecified;
        if (resolved == AppTheme.Unspecified)
            resolved = AppInfo.RequestedTheme == AppTheme.Unspecified ? AppTheme.Light : AppInfo.RequestedTheme;

        WorkoutActivityClassifier.IsDarkTheme = resolved == AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());
        window.Created += OnWindowCreated;
        window.Resumed += OnWindowResumed;
        return window;
    }

    private void OnWindowCreated(object? sender, EventArgs e)
    {
        BeginStartup();
    }

    internal void BeginStartup()
    {
        if (Interlocked.Exchange(ref _startupStarted, 1) != 0)
            return;

        _startupTask = InitializeAndRouteAsync();
    }

    private async Task InitializeAndRouteAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var databaseKeyProvider = scope.ServiceProvider.GetRequiredService<IAppDatabaseKeyProvider>();
            await databaseKeyProvider.InitializeAsync();

            var database = scope.ServiceProvider.GetRequiredService<AppDatabase>();
            await DataLoadScheduler.RunAsync(database.InitializeStartupAsync);
            DatabaseBackupProtection.ExcludeFromBackup(scope.ServiceProvider.GetRequiredService<IDbPathProvider>().GetDatabasePath());

            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var isLoggedIn = await DataLoadScheduler.RunAsync(authService.IsLoggedInAsync);
            if (isLoggedIn)
            {
                var userId = await DataLoadScheduler.RunAsync(authService.GetCurrentUserIdAsync);
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await DataLoadScheduler.RunAsync(() => userRepository.GetByIdAsync(userId));
                if (user is null)
                {
                    await DataLoadScheduler.RunAsync(authService.LogoutAsync);
                    await NavigateFromStartupAsync("//LoginPage");
                    return;
                }

                var shell = _services.GetRequiredService<AppShell>();
                shell.MarkAuthenticationVerified();

                if (!user.IsOnboardingComplete)
                {
                    await NavigateFromStartupAsync($"//{nameof(ProfileSetupPage)}");
                    return;
                }

                await SeedAndPreloadAsync(userId);
                return;
            }

            await NavigateFromStartupAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] InitializeAndRouteAsync failed: {ex}");
            await NavigateFromStartupAsync("//LoginPage");
        }
    }

    private async Task SeedAndPreloadAsync(int userId)
    {
        // Preload owns the preparation barrier, including login/onboarding paths.
        var preloadService = _services.GetRequiredService<IAppPreloadService>();
        await preloadService.PreloadDashboardAsync();
        await NavigateFromStartupAsync("//DashboardPage");

        // Yield to let iOS finish the native VC layout cycle after the Shell
        // structural transition (ShellContent → TabBar) before starting any
        // background ViewModel mutations that could fire PropertyChanged bindings.
        await Task.Yield();

        _ = preloadService.PreloadRemainingAsync();
        _ = ScheduleNotificationsAsync(userId);
    }

    private static Task NavigateFromStartupAsync(string route)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = Shell.Current ?? throw new InvalidOperationException("Shell is not available yet.");
            await shell.GoToAsync(route, false);
        });
    }

    private async Task ScheduleNotificationsAsync(int userId)
    {
        try
        {
            // Notifications own a separate context and await their queries in
            // order. Do not hold the database gate while requesting permission.
            using var scope = _services.CreateScope();
            var cycleNotifications = scope.ServiceProvider.GetRequiredService<ICyclePhaseNotificationService>();
            var checkInNotifications = scope.ServiceProvider.GetRequiredService<IDailyCheckInNotificationService>();
            await cycleNotifications.NotifyIfPhaseChangedAsync(userId);
            await checkInNotifications.ScheduleCheckInReminderAsync(userId);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] ScheduleNotificationsAsync failed: {ex.Message}");
        }
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        _ = HandleDayChangeAsync();
    }

    private async Task HandleDayChangeAsync()
    {
        if (_startupTask is not { IsCompleted: true })
            return;

        try
        {
            var preloadService = _services.GetRequiredService<IAppPreloadService>();
            if (DateTime.Today <= preloadService.LastLoadedDate)
                return;

            await preloadService.RefreshAllAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] HandleDayChangeAsync failed: {ex.Message}");
        }
    }

}
