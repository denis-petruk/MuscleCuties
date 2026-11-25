using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UI.Pages.Auth;
using MuscleCuties.UI.Pages.Cycle;
using MuscleCuties.UI.Pages.Dashboard;
using MuscleCuties.UI.Pages.Nutrition;
using MuscleCuties.UI.Pages.Onboarding;
using MuscleCuties.UI.Pages.Profile;
using MuscleCuties.UI.Pages.Workout;

namespace MuscleCuties.UI;

/// <summary>The five-tab shell. It registers the non-tab routes once, guards navigation on a verified session,</summary>
/// keeps tab icons and titles in step with the theme and language, and measures navigation time.</summary>
public partial class AppShell : Shell
{
    private const double NavigationBudgetMilliseconds = 100;
    private static readonly object RouteRegistrationLock = new();
    private static bool _routesRegistered;
    private readonly ILogger<AppShell> _logger;
    private readonly Stopwatch _navigationStopwatch = new();

    private readonly IServiceProvider _services;
    private bool _authVerified;
    private bool _isRedirectingFromGuard;
    private bool _isResettingProfileRoute;
    private bool _isThemeHandlerAttached;

    /// <summary>Builds the shell with its services, logger and tab icons.</summary>
    public AppShell(
        IServiceProvider services,
        ILogger<AppShell> logger)
    {
        _services = services;
        _logger = logger;
        InitializeComponent();
        ApplyTabIcons(ResolveTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified));
        ApplyLocalizedTitles();

        RegisterRoutesOnce();
    }

    /// <summary>Registers the pushed pages exactly once per process. Shell keeps a static route table, so repeated</summary>
    /// registration would throw on a second window.</summary>
    private static void RegisterRoutesOnce()
    {
        lock (RouteRegistrationLock)
        {
            if (_routesRegistered)
                return;

            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(OtpVerificationPage), typeof(OtpVerificationPage));
            Routing.RegisterRoute(nameof(CyclePhaseDetailPage), typeof(CyclePhaseDetailPage));
            Routing.RegisterRoute(nameof(ProfilePersonalInfoPage), typeof(ProfilePersonalInfoPage));
            Routing.RegisterRoute(nameof(ProfileNutritionSettingsPage), typeof(ProfileNutritionSettingsPage));
            Routing.RegisterRoute(nameof(ProfileWorkoutPreferencesPage), typeof(ProfileWorkoutPreferencesPage));
            Routing.RegisterRoute(nameof(ProfileUnitsDisplayPage), typeof(ProfileUnitsDisplayPage));
            Routing.RegisterRoute(nameof(ProfileFeedbackPage), typeof(ProfileFeedbackPage));
            Routing.RegisterRoute(nameof(ProfilePrivacyPage), typeof(ProfilePrivacyPage));
            Routing.RegisterRoute(nameof(WorkoutSessionPage), typeof(WorkoutSessionPage));
            Routing.RegisterRoute(nameof(StrengthPlanGenerationPage), typeof(StrengthPlanGenerationPage));
            Routing.RegisterRoute(nameof(FoodLogPage), typeof(FoodLogPage));
            Routing.RegisterRoute(nameof(WorkoutHistoryPage), typeof(WorkoutHistoryPage));
            Routing.RegisterRoute(nameof(PhotoJourneyVaultPage), typeof(PhotoJourneyVaultPage));
            Routing.RegisterRoute(nameof(PasswordRecoveryPage), typeof(PasswordRecoveryPage));
            _routesRegistered = true;
        }
    }

    /// <summary>Attaches or detaches the theme handler as the handler changes.</summary>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
            DetachThemeHandler();
        else
            AttachThemeHandler();
    }

    /// <summary>Starts timing the navigation and runs the authentication guard.</summary>
    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        var targetRoute = args.Target?.Location.OriginalString;
        _navigationStopwatch.Restart();
        LogNavigationStarted(_logger, args.Source.ToString());

        if (IsAuthResetRoute(targetRoute))
            _authVerified = false;

        if (_isRedirectingFromGuard ||
            _authVerified ||
            !RequiresAuthenticatedUser(targetRoute))
            return;

        var deferral = args.GetDeferral();
        _ = VerifyAuthenticatedNavigationAsync(args, deferral, targetRoute!);
    }

    /// <summary>Stops timing the navigation and logs how long it presented.</summary>
    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        _navigationStopwatch.Stop();
        var elapsedMilliseconds = _navigationStopwatch.Elapsed.TotalMilliseconds;
        if (Environment.GetEnvironmentVariable("MUSCLECUTIES_PERF_CONSOLE") == "1")
            Console.WriteLine(
                $"[Performance][AppShell] {args.Source} to {CurrentState?.Location} completed in {elapsedMilliseconds:F1} ms.");
        if (elapsedMilliseconds > NavigationBudgetMilliseconds)
            LogNavigationExceededBudget(
                _logger,
                NavigationBudgetMilliseconds,
                elapsedMilliseconds,
                args.Source.ToString());
        else
            LogNavigationPresented(
                _logger,
                elapsedMilliseconds,
                args.Source.ToString());

        if (_isResettingProfileRoute ||
            !IsTabSwitch(args.Source) ||
            !ReferenceEquals(CurrentItem?.CurrentItem, YouTab) ||
            CurrentPage is ProfilePage)
            return;

        _isResettingProfileRoute = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                // Built from the shell's own declared routes, so this page never re-states a
                // route literal that the XAML already owns.
                await GoToAsync($"//{MainTab.Route}/{YouTab.Route}/{ProfileContent.Route}", false);
            }
            catch (Exception ex)
            {
                LogProfileRouteResetFailed(_logger, ex.GetType().Name);
            }
            finally
            {
                _isResettingProfileRoute = false;
            }
        });
    }

    /// <summary>Subscribes to theme changes while the shell has a handler, so a detached shell holds no handler</summary>
    /// reference.</summary>
    private void AttachThemeHandler()
    {
        if (_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        _isThemeHandlerAttached = true;
    }

    /// <summary>Unsubscribes from theme changes when the shell is torn down.</summary>
    private void DetachThemeHandler()
    {
        if (!_isThemeHandlerAttached)
            return;

        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;

        _isThemeHandlerAttached = false;
    }

    /// <summary>Applies the requested theme's tab icons on the main thread.</summary>
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        void Apply()
        {
            ApplyTabIcons(ResolveTheme(e.RequestedTheme));
        }

        if (MainThread.IsMainThread)
            Apply();
        else
            MainThread.BeginInvokeOnMainThread(Apply);
    }

    /// <summary>Picks the light or dark tab artwork, which ships as separate files rather than tinted icons.</summary>
    private void ApplyTabIcons(AppTheme theme)
    {
        var suffix = theme == AppTheme.Dark ? "_dark" : string.Empty;

        TodayTab.Icon = ImageSource.FromFile($"tab_today{suffix}.png");
        CycleTab.Icon = ImageSource.FromFile($"tab_cycle{suffix}.png");
        TrainTab.Icon = ImageSource.FromFile($"tab_train{suffix}.png");
        NutritionTab.Icon = ImageSource.FromFile($"tab_nutrition{suffix}.png");
        YouTab.Icon = ImageSource.FromFile($"tab_you{suffix}.png");
    }

    /// <summary>Sets the five tab titles from the catalog at construction, so a language switch on next launch is</summary>
    /// reflected in the tab bar.</summary>
    private void ApplyLocalizedTitles()
    {
        TodayTab.Title = UiTextCatalog.Get("Tab_Today");
        CycleTab.Title = UiTextCatalog.Get("Tab_Cycle");
        TrainTab.Title = UiTextCatalog.Get("Tab_Train");
        NutritionTab.Title = UiTextCatalog.Get("Tab_Nutrition");
        YouTab.Title = UiTextCatalog.Get("Tab_You");
    }

    /// <summary>The effective theme, falling back to the platform's when the app has not chosen one.</summary>
    private static AppTheme ResolveTheme(AppTheme theme)
    {
        if (theme != AppTheme.Unspecified)
            return theme;

        var appInfoTheme = AppInfo.RequestedTheme;
        return appInfoTheme == AppTheme.Unspecified ? AppTheme.Light : appInfoTheme;
    }

    /// <summary>True for a tab, section or content change, which is how tapping a tab is distinguished from a</summary>
    /// pushed page.</summary>
    private static bool IsTabSwitch(ShellNavigationSource source)
    {
        return source is ShellNavigationSource.ShellItemChanged or
            ShellNavigationSource.ShellSectionChanged or
            ShellNavigationSource.ShellContentChanged;
    }

    /// <summary>Records that the session has been checked, so the guard does not re-verify every navigation.</summary>
    internal void MarkAuthenticationVerified()
    {
        _authVerified = true;
    }

    /// <summary>Confirms the session behind the target route, redirecting when it is gone.</summary>
    private async Task VerifyAuthenticatedNavigationAsync(
        ShellNavigatingEventArgs args,
        ShellNavigatingDeferral deferral,
        string targetRoute)
    {
        string? redirectRoute = null;
        try
        {
            var authState = await DataLoadScheduler.RunAsync(async () =>
            {
                using var scope = _services.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var state = await authService.GetCurrentUserStateAsync();
                // F07r: distinguish "no account" from "cached onboarding flag, valid session".
                return (State: state, HasSession: state is not null && await authService.IsLoggedInAsync());
            });
            var currentUser = authState.State;
            var decision = StartupRoutingRule.Resolve(
                stateReadFailed: currentUser is null && authState.HasSession,
                hasSession: authState.HasSession,
                onboardingIncomplete: currentUser is not null && !currentUser.IsOnboardingComplete);
            if (decision == StartupRoute.StartupError)
            {
                args.Cancel();
                redirectRoute = "//AppStartupPage";
            }
            else if (decision == StartupRoute.Login)
            {
                args.Cancel();
                redirectRoute = "//LoginPage";
            }
            else if (decision == StartupRoute.ProfileSetup && !AllowsOnboardingRoute(targetRoute))
            {
                args.Cancel();
                redirectRoute = $"//{nameof(ProfileSetupPage)}";
            }
            else
            {
                _authVerified = true;
            }
        }
        catch (Exception ex)
        {
            args.Cancel();
            LogNavigationPreparationFailed(_logger, ex.GetType().Name);
        }
        finally
        {
            deferral.Complete();
        }

        if (string.IsNullOrWhiteSpace(redirectRoute))
            return;

        _isRedirectingFromGuard = true;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () => { await GoToAsync(redirectRoute, false); });
        }
        catch (Exception ex)
        {
            LogNavigationGuardRedirectFailed(_logger, ex.GetType().Name);
        }
        finally
        {
            _isRedirectingFromGuard = false;
        }
    }

    /// <summary>True for the app's own screens. The privacy page stays reachable without a session, and the</summary>
    /// onboarding screens stay reachable while a registration is in flight.</summary>
    private bool RequiresAuthenticatedUser(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;

        if (ContainsRoute(route, nameof(ProfilePrivacyPage)))
            return false;

        if (AllowsOnboardingRoute(route))
        {
            var pendingRegistration = _services.GetService<IPendingRegistrationService>();
            if (pendingRegistration?.HasPendingRegistration == true)
                return false;
        }

        return ContainsRoute(route, "DashboardPage") ||
               ContainsRoute(route, "QuizPage") ||
               ContainsRoute(route, "ProfileSetupPage") ||
               ContainsRoute(route, "Cycle") ||
               ContainsRoute(route, "Workout") ||
               ContainsRoute(route, "StrengthPlanGeneration") ||
               ContainsRoute(route, "Nutrition") ||
               ContainsRoute(route, "FoodLog") ||
               ContainsRoute(route, "Profile") ||
               ContainsRoute(route, "PhotoJourneyVault") ||
               ContainsRoute(route, "MainTab");
    }

    /// <summary>Case-insensitive route segment test; the routes here are shell-shaped, not exact strings.</summary>
    private static bool ContainsRoute(string route, string segment)
    {
        return route.Contains(segment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The screens an incomplete onboarding may still open: quiz, profile setup and email verification.</summary>
    private static bool AllowsOnboardingRoute(string route)
    {
        return ContainsRoute(route, nameof(QuizPage)) ||
               ContainsRoute(route, nameof(ProfileSetupPage)) ||
               ContainsRoute(route, "OtpVerification");
    }

    /// <summary>True when the target is an auth screen, which clears the verified flag so the next guarded</summary>
    /// navigation checks again.</summary>
    private static bool IsAuthResetRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;

        return ContainsRoute(route, "Login") ||
               ContainsRoute(route, "Register") ||
               ContainsRoute(route, "OtpVerification") ||
               ContainsRoute(route, "PasswordRecovery");
    }

    /// <summary>Logs the start of a navigation.</summary>
    [LoggerMessage(LogLevel.Information, "Navigation started. Source={Source}.")]
    private static partial void LogNavigationStarted(ILogger logger, string source);

    /// <summary>Logs a navigation that ran past its time budget.</summary>
    [LoggerMessage(LogLevel.Warning,
        "Navigation exceeded the {BudgetMilliseconds} ms budget: {ElapsedMilliseconds:F1} ms. Source={Source}.")]
    private static partial void LogNavigationExceededBudget(ILogger logger, double budgetMilliseconds,
        double elapsedMilliseconds, string source);

    /// <summary>Logs how long a navigation took to present.</summary>
    [LoggerMessage(LogLevel.Information, "Navigation presented in {ElapsedMilliseconds:F1} ms. Source={Source}.")]
    private static partial void LogNavigationPresented(ILogger logger, double elapsedMilliseconds, string source);

    /// <summary>Logs a navigation whose preparation failed.</summary>
    [LoggerMessage(LogLevel.Error, "Navigation preparation failed ({ExceptionType}).")]
    private static partial void LogNavigationPreparationFailed(ILogger logger, string exceptionType);

    /// <summary>Logs a navigation guard redirect that failed.</summary>
    [LoggerMessage(LogLevel.Error, "Navigation guard redirect failed ({ExceptionType}).")]
    private static partial void LogNavigationGuardRedirectFailed(ILogger logger, string exceptionType);

    /// <summary>Logs a profile route reset that failed.</summary>
    [LoggerMessage(LogLevel.Error, "Profile route reset failed ({ExceptionType}).")]
    private static partial void LogProfileRouteResetFailed(ILogger logger, string exceptionType);
}
