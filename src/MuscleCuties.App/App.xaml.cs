using System.Diagnostics;
using Microsoft.Data.Sqlite;
using MuscleCuties.App.Pages.Dashboard;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Startup;
using MuscleCuties.App.Services.Security;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.Core.Data;
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
    private AppStartupPage? _startupPage;
    private bool _startupFailed;

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
        window.Stopped += OnWindowStopped;
        return window;
    }

    private void OnWindowCreated(object? sender, EventArgs e)
    {
        BeginStartup();
    }

    internal void BeginStartup(AppStartupPage? startupPage = null)
    {
        if (startupPage is not null)
        {
            _startupPage = startupPage;
            if (_startupFailed)
                startupPage.ShowStartupError();
        }

        if (Interlocked.Exchange(ref _startupStarted, 1) != 0)
            return;

        _startupTask = InitializeAndRouteAsync();
    }

    internal async Task RetryStartupAsync()
    {
        if (_startupTask is { } previousStartup)
            await previousStartup;

        _startupFailed = false;
        Interlocked.Exchange(ref _startupStarted, 0);
        BeginStartup();
    }

    internal void DetachStartupPage(AppStartupPage startupPage)
    {
        if (ReferenceEquals(_startupPage, startupPage))
            _startupPage = null;
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
            var currentUser = await DataLoadScheduler.RunAsync(authService.GetCurrentUserStateAsync);
            if (currentUser is not null)
            {
                var shell = _services.GetRequiredService<AppShell>();
                shell.MarkAuthenticationVerified();

                if (!currentUser.IsOnboardingComplete)
                {
                    await NavigateFromStartupAsync($"//{nameof(ProfileSetupPage)}");
                    return;
                }

                await NavigateToDashboardAsync(currentUser.UserId);
                return;
            }

            await NavigateFromStartupAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] InitializeAndRouteAsync failed ({ex.GetType().Name}).");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _startupFailed = true;
                _startupPage?.ShowStartupError();
            });
        }
    }

    private async Task NavigateToDashboardAsync(int userId)
    {
        await NavigateFromStartupAsync("//DashboardPage");

        // Hidden-tab preloads share the same SQLite gate as the visible page.
        // Let the Dashboard finish its first data load before competing for it.
        var dashboardPage = Shell.Current?.CurrentPage as DashboardPage;
        if (dashboardPage is null)
        {
            Trace.WriteLine("[Startup] Dashboard page was unavailable for preload coordination.");
            return;
        }

        await dashboardPage.InitialLoadCompleted;

        var preloadService = _services.GetRequiredService<IAppPreloadService>();
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
            await DataLoadScheduler.RunAsync(() => cycleNotifications.NotifyIfPhaseChangedAsync(userId));
            await DataLoadScheduler.RunAsync(() => checkInNotifications.ScheduleCheckInReminderAsync(userId));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] ScheduleNotificationsAsync failed ({ex.GetType().Name}).");
        }
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        _ = HandleDayChangeAsync();
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        _ = Task.Run(SqliteConnection.ClearAllPools);
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
            Trace.WriteLine($"[Startup] HandleDayChangeAsync failed ({ex.GetType().Name}).");
        }
    }

}
