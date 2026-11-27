using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Pages.Dashboard;
using MuscleCuties.UI.Pages.Onboarding;
using MuscleCuties.UI.Pages.Startup;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.Services.Notifications;
using MuscleCuties.UI.Services.Security;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UI.ViewModels.Profile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.UI;

/// <summary>Owns the app's lifetime: creates the window and shell, runs startup routing once, and reacts to</summary>
/// foreground, background and day changes.</summary>
public partial class App : Application, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IBackendSessionLostSignal? _sessionLostSignal;
    private bool _startupFailed;
    private bool _startupFailedToUnlockData;
    private AppStartupPage? _startupPage;
    private int _startupStarted;
    private Task? _startupTask;
    private DateTime _lastDashboardRefreshDate = DateTime.MinValue;
    private bool _isForeground;
    private CancellationTokenSource? _foregroundCancellation;
    /// <summary>Raised when the app leaves the foreground, so a running workout timer can be checkpointed before</summary>
    /// the process is suspended.</summary>
    internal event EventHandler? WorkoutTimingSuspending;

    /// <summary>Builds the app, wiring the theme and session-loss handlers.</summary>
    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        SyncActivityTheme();
        RequestedThemeChanged += (_, e) => SyncActivityTheme(e.RequestedTheme);
        _sessionLostSignal = services.GetService<IBackendSessionLostSignal>();
        if (_sessionLostSignal is not null)
            _sessionLostSignal.SessionLost += OnSessionLost;
    }

    /// <summary>
    /// U-01: a rejected refresh cleared the session. Every screen would otherwise keep failing with
    /// 401s, so the app returns to sign-in once; the signal is re-armed after the switch.
    /// </summary>
    private void OnSessionLost(object? sender, EventArgs e) => _ = ReturnToSignInAsync();

    /// <summary>Moves the app back to sign-in after the session was rejected, and re-arms the session signal so</summary>
    /// the next rejection is handled again.</summary>
    private async Task ReturnToSignInAsync()
    {
        try
        {
            await ActivateRootAsync("LoginPage");
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[Session] Return to sign-in failed ({exception.GetType().Name}).");
        }
        finally
        {
            _sessionLostSignal?.Acknowledge();
        }
    }

    /// <summary>Unhooks the session-loss handler and releases the foreground cancellation.</summary>
    public void Dispose()
    {
        if (_sessionLostSignal is not null)
            _sessionLostSignal.SessionLost -= OnSessionLost;
        _foregroundCancellation?.Cancel();
        _foregroundCancellation?.Dispose();
        _foregroundCancellation = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>Publishes the effective theme to the workout palette, resolving the system default when the</summary>
    /// requested theme is unspecified.</summary>
    private static void SyncActivityTheme(AppTheme? theme = null)
    {
        var resolved = theme ?? Current?.RequestedTheme ?? AppTheme.Unspecified;
        if (resolved == AppTheme.Unspecified)
            resolved = AppInfo.RequestedTheme == AppTheme.Unspecified ? AppTheme.Light : AppInfo.RequestedTheme;

        WorkoutPresentationPalette.IsDarkTheme = resolved == AppTheme.Dark;
    }

    /// <summary>Creates the window that hosts the shell.</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());
        window.Created += OnWindowCreated;
        window.Resumed += OnWindowResumed;
        window.Stopped += OnWindowStopped;
        return window;
    }

    /// <summary>First window: the app is foreground from now on, and startup routing begins exactly once.</summary>
    private void OnWindowCreated(object? sender, EventArgs e)
    {
        _isForeground = true;
        _foregroundCancellation = new CancellationTokenSource();
        BeginStartup();
    }

    /// <summary>Starts startup routing once per launch. A page handed in later still receives the earlier</summary>
    /// failure, so a retry affordance is never lost to a race.</summary>
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

    /// <summary>Waits for the failed attempt to finish, clears the failure, and starts routing again.</summary>
    internal async Task RetryStartupAsync()
    {
        if (_startupTask is { } previousStartup)
            await previousStartup;

        _startupFailed = false;
        Interlocked.Exchange(ref _startupStarted, 0);
        BeginStartup();
    }

    /// <summary>Signs out, purges the local account state and photo cache, then retries startup. Cleanup errors are</summary>
    /// logged rather than blocking the restart.</summary>
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

    /// <summary>Forgets the startup page once it is gone, so a later failure is not reported to a disposed page.</summary>
    internal void DetachStartupPage(AppStartupPage startupPage)
    {
        if (ReferenceEquals(_startupPage, startupPage))
            _startupPage = null;
    }

    /// <summary>The startup decision: clear stale keychain entries on a fresh install, read the account, then route</summary>
    /// to setup, dashboard or sign-in. A stored session whose state cannot be read is a failed verification, not</summary>
    /// a sign-out.</summary>
    private async Task InitializeAndRouteAsync()
    {
        var unlockingData = false;
        try
        {
            if (!Preferences.Default.ContainsKey("mc_has_run_before"))
            {
                try
                {
                    SecureStorage.Default.RemoveAll();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Startup] SecureStorage.RemoveAll on fresh install failed ({ex.GetType().Name}).");
                }
                finally
                {
                    Preferences.Default.Set("mc_has_run_before", true);
                }
            }

            using var scope = _services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var currentUser = await DataLoadScheduler.RunAsync(authService.GetCurrentUserStateAsync);
            if (currentUser is not null)
            {
                await _services.GetRequiredService<IPhotoCacheAccountLifecycle>()
                    .ActivateCurrentAccountAsync();
                var shell = Shell.Current as AppShell ?? _services.GetRequiredService<AppShell>();
                shell.MarkAuthenticationVerified();

                var startupRoute = StartupRoutingRule.Resolve(
                    stateReadFailed: false,
                    hasSession: await authService.IsLoggedInAsync(),
                    onboardingIncomplete: !currentUser.IsOnboardingComplete);
                if (startupRoute == StartupRoute.ProfileSetup)
                {
                    await ActivateRootAsync(nameof(ProfileSetupPage));
                    return;
                }
                if (startupRoute == StartupRoute.Dashboard && !currentUser.IsOnboardingComplete)
                {
                    await NavigateToDashboardAsync(currentUser.UserId);
                    return;
                }
                if (startupRoute == StartupRoute.Login)
                {
                    await ActivateRootAsync("LoginPage");
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

            if (await authService.IsLoggedInAsync())
            {
                // A stored session whose state cannot be resolved is a failed verification.
                _startupFailed = true;
                _startupPage?.ShowStartupError(false);
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

    /// <summary>Activates the dashboard, waits for its first load so hidden-tab preloads do not compete with it,</summary>
    /// then schedules notifications.</summary>
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
    /// <summary>Switches the shell's root. Signing in clears the half-finished workout-preference draft and the</summary>
    /// previous account's check-in state; any other root marks the session as verified and refreshes the</summary>
    /// workout time zone while foregrounded.</summary>
    internal async Task ActivateRootAsync(string route)
    {
        if (route == "LoginPage")
            _services.GetRequiredService<INavigationContextService>()
                .TryTake<WorkoutPreferencesDto>(ProfileWorkoutPreferencesViewModel.DraftPreferencesContextKey, out _);
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = Shell.Current as AppShell
                ?? throw new InvalidOperationException("The active app shell is unavailable.");
            if (route == "LoginPage" && _lastDashboardRefreshDate != DateTime.MinValue)
            {
                _services.GetRequiredService<DashboardViewModel>().ResetCheckInForAccountChange();
                _lastDashboardRefreshDate = DateTime.MinValue;
            }
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

    /// <summary>Waits briefly for the dashboard to become the current page, returning null instead of blocking</summary>
    /// startup when it never does.</summary>
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

    /// <summary>Runs the cycle-phase and check-in reminders through the load scheduler, so they cannot overlap the</summary>
    /// startup data reads.</summary>
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

    /// <summary>Back in the foreground: restarts the foreground cancellation token, refreshes the workout time</summary>
    /// zone, and catches up if the date changed while the app was away.</summary>
    private void OnWindowResumed(object? sender, EventArgs e)
    {
        _isForeground = true;
        _foregroundCancellation?.Cancel();
        _foregroundCancellation = new CancellationTokenSource();
        _ = UpdateForegroundCalendarAsync();
        _ = HandleDayChangeAsync();
    }

    /// <summary>Leaving the foreground: cancels in-flight foreground work and tells a running workout to checkpoint</summary>
    /// its timer before suspension.</summary>
    private void OnWindowStopped(object? sender, EventArgs e)
    {
        _isForeground = false;
        _foregroundCancellation?.Cancel();
        WorkoutTimingSuspending?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sends the device time zone to the server, but only while foregrounded and signed in, so a</summary>
    /// backgrounded app does not write.</summary>
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

    /// <summary>After midnight, invalidates and reloads the dashboard so the first screen of the day is not</summary>
    /// yesterday's. Does nothing until the initial startup routing has completed.</summary>
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
