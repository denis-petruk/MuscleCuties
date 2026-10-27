using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Core.Data;
using MuscleCuties.UI.Pages.Dashboard;
using MuscleCuties.UI.Pages.Onboarding;
using MuscleCuties.UI.Pages.Startup;
using MuscleCuties.UI.Services.Notifications;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Security;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Profile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.UI;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private bool _startupFailed;
    private bool _startupFailedToUnlockData;
    private AppStartupPage? _startupPage;
    private int _startupStarted;
    private Task? _startupTask;
    private DateTime _lastDashboardRefreshDate = DateTime.MinValue;
    private bool _isForeground;
    private CancellationTokenSource? _foregroundCancellation;
    internal event EventHandler? WorkoutTimingSuspending;

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

        WorkoutPresentationPalette.IsDarkTheme = resolved == AppTheme.Dark;
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
        _isForeground = true;
        _foregroundCancellation = new CancellationTokenSource();
        BeginStartup();
    }

    internal void BeginStartup(AppStartupPage? startupPage = null)
    {
        if (startupPage is not null)
        {
            _startupPage = startupPage;
            if (_startupFailed)
                startupPage.ShowStartupError(_startupFailedToUnlockData);
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

    internal async Task ResetAndRestartAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var cloudGateway = scope.ServiceProvider.GetService<MobileJourneyCloudGateway>();
            if (cloudGateway is not null)
            {
                await cloudGateway.LogoutAndPurgeLocalStateAsync().ConfigureAwait(false);
            }

            var photoCache = scope.ServiceProvider.GetService<ILocalPhotoCacheService>();
            if (photoCache is not null)
            {
                await photoCache.ClearAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] ResetAndRestartAsync cleanup exception ({ex.GetType().Name}).");
        }

        await RetryStartupAsync();
    }

    internal void DetachStartupPage(AppStartupPage startupPage)
    {
        if (ReferenceEquals(_startupPage, startupPage))
            _startupPage = null;
    }

    private async Task InitializeAndRouteAsync()
    {
        var unlockingData = false;
        try
        {
            using var scope = _services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var currentUser = await DataLoadScheduler.RunAsync(authService.GetCurrentUserStateAsync);
            if (currentUser is not null)
            {
                await _services.GetRequiredService<IPhotoCacheAccountLifecycle>()
                    .ActivateCurrentAccountAsync();
                var shell = Shell.Current as AppShell ?? _services.GetRequiredService<AppShell>();
                shell.MarkAuthenticationVerified();

                if (!currentUser.IsOnboardingComplete)
                {
                    await ActivateRootAsync(nameof(ProfileSetupPage));
                    return;
                }

                var initialRoute = Environment.GetEnvironmentVariable("MUSCLECUTIES_INITIAL_ROUTE");
                if (!string.IsNullOrWhiteSpace(initialRoute))
                {
                    try
                    {
                        await shell.GoToAsync(initialRoute, false);
                        _ = ScheduleNotificationsAsync(currentUser.UserId);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[Startup] Failed to navigate to initial route {initialRoute}: {ex.Message}");
                    }
                }

                await NavigateToDashboardAsync(currentUser.UserId);
                return;
            }

            await ActivateRootAsync("LoginPage");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] InitializeAndRouteAsync failed ({ex.GetType().Name}).");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _startupFailed = true;
                _startupFailedToUnlockData = unlockingData;
                _startupPage?.ShowStartupError(unlockingData);
            });
        }
    }

    private async Task NavigateToDashboardAsync(int userId)
    {
        await ActivateRootAsync(nameof(DashboardPage));

        // Let the Dashboard finish its first data load before hidden-tab preloads compete for it.
        var dashboardPage = await WaitForDashboardPageAsync();
        if (dashboardPage is null)
        {
            Trace.WriteLine("[Startup] Dashboard page was unavailable for preload coordination.");
            _ = ScheduleNotificationsAsync(userId);
            return;
        }

        await dashboardPage.InitialLoadCompleted;

        _ = ScheduleNotificationsAsync(userId);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Exposed as an application navigation operation to page coordinators.")]
    internal async Task ActivateRootAsync(string route)
    {
        if (route == "LoginPage")
            _services.GetRequiredService<INavigationContextService>()
                .TryTake<WorkoutPreferencesDto>(ProfileWorkoutPreferencesViewModel.DraftPreferencesContextKey, out _);
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = Shell.Current as AppShell
                ?? throw new InvalidOperationException("The active app shell is unavailable.");
            if (route != "LoginPage")
                shell.MarkAuthenticationVerified();
            await shell.GoToAsync($"//{route}", false);
            if (route == nameof(DashboardPage))
                _lastDashboardRefreshDate = DateTime.Today;
        });
        Trace.WriteLine($"[Navigation] {route} selected as the app root.");
        if (_isForeground && route != "LoginPage")
            _ = UpdateForegroundCalendarAsync();
    }

    internal static async Task<DashboardPage?> WaitForDashboardPageAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var page = await MainThread.InvokeOnMainThreadAsync(
                () => Shell.Current?.CurrentPage as DashboardPage);
            if (page is not null)
                return page;

            await Task.Delay(50);
        }

        return null;
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
        _isForeground = true;
        _foregroundCancellation?.Cancel();
        _foregroundCancellation = new CancellationTokenSource();
        _ = UpdateForegroundCalendarAsync();
        _ = HandleDayChangeAsync();
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        _isForeground = false;
        _foregroundCancellation?.Cancel();
        WorkoutTimingSuspending?.Invoke(this, EventArgs.Empty);
    }

    private async Task UpdateForegroundCalendarAsync()
    {
        try
        {
            var cancellationToken = _foregroundCancellation?.Token ?? CancellationToken.None;
            var tokenStore = _services.GetRequiredService<IBackendSessionTokenStore>();
            var tokens = await tokenStore.ReadTokensAsync(cancellationToken);
            if (!_isForeground || tokens is null)
                return;
            await _services.GetRequiredService<IWorkoutAccountCalendarClient>().UpdateForegroundTimeZoneAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[WorkoutCalendar] Foreground update failed ({exception.GetType().Name}).");
        }
    }

    private async Task HandleDayChangeAsync()
    {
        if (_startupTask is not { IsCompleted: true })
            return;

        try
        {
            var preloadService = _services.GetRequiredService<IAppPreloadService>();
            if (DateTime.Today <= _lastDashboardRefreshDate)
                return;

            preloadService.InvalidateAll();
            await preloadService.PreloadDashboardAsync();
            _lastDashboardRefreshDate = DateTime.Today;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Startup] HandleDayChangeAsync failed ({ex.GetType().Name}).");
        }
    }
}
