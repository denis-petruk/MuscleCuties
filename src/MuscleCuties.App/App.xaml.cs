using System.Diagnostics;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private int _referenceSeedStarted;
    private int _startupStarted;

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

        _ = InitializeAndRouteAsync();
    }

    private async Task InitializeAndRouteAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDatabase>();
            await Task.Run(database.InitializeStartupAsync);

            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var isLoggedIn = await Task.Run(authService.IsLoggedInAsync);
            if (isLoggedIn)
            {
                var userId = await Task.Run(authService.GetCurrentUserIdAsync);
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await Task.Run(() => userRepository.GetByIdAsync(userId));
                if (user is null)
                {
                    await Task.Run(authService.LogoutAsync);
                    await NavigateFromStartupAsync("//LoginPage");
                    StartReferenceDataSeed();
                    return;
                }

                var shell = _services.GetRequiredService<AppShell>();
                shell.MarkAuthenticationVerified();

                if (!user.IsOnboardingComplete)
                {
                    await NavigateFromStartupAsync($"//{nameof(ProfileSetupPage)}");
                    StartReferenceDataSeed();
                    return;
                }

                await SeedAndPreloadAsync(database, userId);
                return;
            }

            await NavigateFromStartupAsync("//LoginPage");
            StartReferenceDataSeed();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] InitializeAndRouteAsync failed: {ex}");
            await NavigateFromStartupAsync("//LoginPage");
        }
    }

    private async Task SeedAndPreloadAsync(AppDatabase database, int userId)
    {
        await Task.Run(database.SeedDeferredReferenceDataAsync);

        var preloadService = _services.GetRequiredService<IAppPreloadService>();
        await preloadService.PreloadDashboardAsync();
        await NavigateFromStartupAsync("//DashboardPage");
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

    private void StartReferenceDataSeed()
    {
        if (Interlocked.Exchange(ref _referenceSeedStarted, 1) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<AppDatabase>();
                await database.SeedDeferredReferenceDataAsync();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Startup] StartReferenceDataSeed failed: {ex.Message}");
            }
        });
    }
}
