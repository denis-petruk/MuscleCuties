using System.Diagnostics;
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
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI;

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

    private static void RegisterRoutesOnce()
    {
        lock (RouteRegistrationLock)
        {
            if (_routesRegistered)
                return;

            Routing.RegisterRoute(nameof(DailyCheckInPage), typeof(DailyCheckInPage));
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(OtpVerificationPage), typeof(OtpVerificationPage));
            Routing.RegisterRoute(nameof(CyclePhaseDetailPage), typeof(CyclePhaseDetailPage));
            Routing.RegisterRoute(nameof(ProfilePersonalInfoPage), typeof(ProfilePersonalInfoPage));
            Routing.RegisterRoute(nameof(ProfileNutritionSettingsPage), typeof(ProfileNutritionSettingsPage));
            Routing.RegisterRoute(nameof(ProfileWorkoutPreferencesPage), typeof(ProfileWorkoutPreferencesPage));
            Routing.RegisterRoute(nameof(ProfileHealthSyncPage), typeof(ProfileHealthSyncPage));
            Routing.RegisterRoute(nameof(ProfileUnitsDisplayPage), typeof(ProfileUnitsDisplayPage));
            Routing.RegisterRoute(nameof(ProfileFeedbackPage), typeof(ProfileFeedbackPage));
            Routing.RegisterRoute(nameof(ProfilePrivacyPage), typeof(ProfilePrivacyPage));
            Routing.RegisterRoute(nameof(PhaseAdjustmentReviewPageV010), typeof(PhaseAdjustmentReviewPageV010));
            Routing.RegisterRoute(nameof(WorkoutSessionPage), typeof(WorkoutSessionPage));
            Routing.RegisterRoute(nameof(StrengthPlanGenerationPage), typeof(StrengthPlanGenerationPage));
            Routing.RegisterRoute(nameof(FoodLogPage), typeof(FoodLogPage));
            Routing.RegisterRoute(nameof(WorkoutHistoryPage), typeof(WorkoutHistoryPage));
            Routing.RegisterRoute(nameof(PhotoJourneyVaultPage), typeof(PhotoJourneyVaultPage));
            Routing.RegisterRoute(nameof(PasswordRecoveryPage), typeof(PasswordRecoveryPage));
            _routesRegistered = true;
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
            DetachThemeHandler();
        else
            AttachThemeHandler();
    }

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
                await GoToAsync("//MainTab/YouTab/ProfilePage", false);
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

    private void AttachThemeHandler()
    {
        if (_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        _isThemeHandlerAttached = true;
    }

    private void DetachThemeHandler()
    {
        if (!_isThemeHandlerAttached)
            return;

        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;

        _isThemeHandlerAttached = false;
    }

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

    private void ApplyTabIcons(AppTheme theme)
    {
        var suffix = theme == AppTheme.Dark ? "_dark" : string.Empty;

        TodayTab.Icon = ImageSource.FromFile($"tab_today{suffix}.png");
        CycleTab.Icon = ImageSource.FromFile($"tab_cycle{suffix}.png");
        TrainTab.Icon = ImageSource.FromFile($"tab_train{suffix}.png");
        NutritionTab.Icon = ImageSource.FromFile($"tab_nutrition{suffix}.png");
        YouTab.Icon = ImageSource.FromFile($"tab_you{suffix}.png");
    }

    private void ApplyLocalizedTitles()
    {
        var localization = _services.GetService<ILocalizationService>();
        if (localization is null)
            return;

        TodayTab.Title = localization["Tab_Today"];
        CycleTab.Title = localization["Tab_Cycle"];
        TrainTab.Title = localization["Tab_Train"];
        NutritionTab.Title = localization["Tab_Nutrition"];
        YouTab.Title = localization["Tab_You"];
    }

    private static AppTheme ResolveTheme(AppTheme theme)
    {
        if (theme != AppTheme.Unspecified)
            return theme;

        var appInfoTheme = AppInfo.RequestedTheme;
        return appInfoTheme == AppTheme.Unspecified ? AppTheme.Light : appInfoTheme;
    }

    private static bool IsTabSwitch(ShellNavigationSource source)
    {
        return source is ShellNavigationSource.ShellItemChanged or
            ShellNavigationSource.ShellSectionChanged or
            ShellNavigationSource.ShellContentChanged;
    }

    internal void MarkAuthenticationVerified()
    {
        _authVerified = true;
    }

    internal void SelectRootBeforeWindowAttachment(string route)
    {
        if (route != nameof(LoginPage))
            MarkAuthenticationVerified();

        var root = route == nameof(DashboardPage)
            ? MainTab
            : Items.FirstOrDefault(item => item.Items.Any(section =>
                section.Items.Any(content => content.Route == route)));
        if (root is null)
            throw new InvalidOperationException($"Unknown app root: {route}.");

        // A newly mounted Shell must not contain the onboarding and tab roots
        // together: iOS can enter a native layout loop during their transition.
        foreach (var item in Items.ToArray())
            if (!ReferenceEquals(item, root))
                Items.Remove(item);

        if (ReferenceEquals(root, MainTab))
        {
            MainTab.CurrentItem = TodayTab;
            TodayTab.CurrentItem = DashboardContent;
        }

        CurrentItem = root;
    }

    private async Task VerifyAuthenticatedNavigationAsync(
        ShellNavigatingEventArgs args,
        ShellNavigatingDeferral deferral,
        string targetRoute)
    {
        string? redirectRoute = null;
        try
        {
            var currentUser = await DataLoadScheduler.RunAsync(async () =>
            {
                using var scope = _services.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                return await authService.GetCurrentUserStateAsync();
            });
            if (currentUser is null)
            {
                args.Cancel();
                redirectRoute = "//LoginPage";
            }
            else if (!currentUser.IsOnboardingComplete && !AllowsOnboardingRoute(targetRoute))
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

        return ContainsRoute(route, "DailyCheckIn") ||
               ContainsRoute(route, "DashboardPage") ||
               ContainsRoute(route, "QuizPage") ||
               ContainsRoute(route, "ProfileSetupPage") ||
               ContainsRoute(route, "Cycle") ||
               ContainsRoute(route, "Workout") ||
               ContainsRoute(route, "StrengthPlanGeneration") ||
               ContainsRoute(route, "PhaseAdjustmentReview") ||
               ContainsRoute(route, "Nutrition") ||
               ContainsRoute(route, "FoodLog") ||
               ContainsRoute(route, "Profile") ||
               ContainsRoute(route, "PhotoJourneyVault") ||
               ContainsRoute(route, "MainTab");
    }

    private static bool ContainsRoute(string route, string segment)
    {
        return route.Contains(segment, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsOnboardingRoute(string route)
    {
        return ContainsRoute(route, nameof(QuizPage)) ||
               ContainsRoute(route, nameof(ProfileSetupPage)) ||
               ContainsRoute(route, "OtpVerification");
    }

    private static bool IsAuthResetRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;

        return ContainsRoute(route, "Login") ||
               ContainsRoute(route, "Register") ||
               ContainsRoute(route, "OtpVerification") ||
               ContainsRoute(route, "PasswordRecovery");
    }

    [LoggerMessage(LogLevel.Information, "Navigation started. Source={Source}.")]
    private static partial void LogNavigationStarted(ILogger logger, string source);

    [LoggerMessage(LogLevel.Warning,
        "Navigation exceeded the {BudgetMilliseconds} ms budget: {ElapsedMilliseconds:F1} ms. Source={Source}.")]
    private static partial void LogNavigationExceededBudget(ILogger logger, double budgetMilliseconds,
        double elapsedMilliseconds, string source);

    [LoggerMessage(LogLevel.Information, "Navigation presented in {ElapsedMilliseconds:F1} ms. Source={Source}.")]
    private static partial void LogNavigationPresented(ILogger logger, double elapsedMilliseconds, string source);

    [LoggerMessage(LogLevel.Error, "Navigation preparation failed ({ExceptionType}).")]
    private static partial void LogNavigationPreparationFailed(ILogger logger, string exceptionType);

    [LoggerMessage(LogLevel.Error, "Navigation guard redirect failed ({ExceptionType}).")]
    private static partial void LogNavigationGuardRedirectFailed(ILogger logger, string exceptionType);

    [LoggerMessage(LogLevel.Error, "Profile route reset failed ({ExceptionType}).")]
    private static partial void LogProfileRouteResetFailed(ILogger logger, string exceptionType);
}
