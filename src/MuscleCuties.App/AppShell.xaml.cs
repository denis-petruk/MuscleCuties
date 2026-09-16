using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MuscleCuties.App.Pages.Auth;
using MuscleCuties.App.Pages.Cycle;
using MuscleCuties.App.Pages.Dashboard;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Profile;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AppShell> _logger;
    private bool _isRedirectingFromGuard;
    private bool _isResettingProfileRoute;
    private bool _isThemeHandlerAttached;
    private bool _authVerified;

    public AppShell(
        IServiceProvider services,
        ILogger<AppShell> logger)
    {
        _services = services;
        _logger = logger;
        InitializeComponent();
        AttachThemeHandler();
        ApplyTabIcons(ResolveTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified));

        Routing.RegisterRoute(nameof(DailyCheckInPage), typeof(DailyCheckInPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(CyclePhaseDetailPage), typeof(CyclePhaseDetailPage));
        Routing.RegisterRoute(nameof(ProfilePersonalInfoPage), typeof(ProfilePersonalInfoPage));
        Routing.RegisterRoute(nameof(ProfileNutritionSettingsPage), typeof(ProfileNutritionSettingsPage));
        Routing.RegisterRoute(nameof(ProfileWorkoutPreferencesPage), typeof(ProfileWorkoutPreferencesPage));
        Routing.RegisterRoute(nameof(ProfileHealthSyncPage), typeof(ProfileHealthSyncPage));
        Routing.RegisterRoute(nameof(ProfileUnitsDisplayPage), typeof(ProfileUnitsDisplayPage));
        Routing.RegisterRoute(nameof(ProfileFeedbackPage), typeof(ProfileFeedbackPage));
        Routing.RegisterRoute(nameof(ProfilePrivacyPage), typeof(ProfilePrivacyPage));
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        var targetRoute = args.Target?.Location.OriginalString;
        _logger.LogInformation("Navigation started. Target={TargetRoute}, Source={Source}.", targetRoute, args.Source);

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
        _logger.LogInformation(
            "Navigation completed. Current={CurrentRoute}, Source={Source}.",
            args.Current?.Location.OriginalString,
            args.Source);

        if (_isResettingProfileRoute ||
            !IsTabSwitch(args.Source) ||
            CurrentItem?.CurrentItem?.Route != "YouTab")
            return;

        var location = CurrentState?.Location?.OriginalString ?? string.Empty;
        if (location.EndsWith(nameof(ProfilePage), StringComparison.OrdinalIgnoreCase))
            return;

        _isResettingProfileRoute = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await GoToAsync("//ProfilePage", false);
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

    private async Task VerifyAuthenticatedNavigationAsync(
        ShellNavigatingEventArgs args,
        ShellNavigatingDeferral deferral,
        string targetRoute)
    {
        string? redirectRoute = null;
        try
        {
            using var scope = _services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var userId = await Task.Run(authService.GetCurrentUserIdAsync);
            if (userId <= 0)
            {
                args.Cancel();
                redirectRoute = "//LoginPage";
            }
            else
            {
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await Task.Run(() => userRepository.GetByIdAsync(userId));
                if (user is null)
                {
                    await Task.Run(authService.LogoutAsync);
                    args.Cancel();
                    redirectRoute = "//LoginPage";
                }
                else if (!user.IsOnboardingComplete && !AllowsOnboardingRoute(targetRoute))
                {
                    args.Cancel();
                    redirectRoute = $"//{nameof(ProfileSetupPage)}";
                }
                else
                {
                    _authVerified = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation preparation failed for route {Route}.", targetRoute);
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
            _logger.LogError(ex, "Navigation guard redirect failed for route {Route}.", redirectRoute);
        }
        finally
        {
            _isRedirectingFromGuard = false;
        }
    }

    private static bool RequiresAuthenticatedUser(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;

        return ContainsRoute(route, "DailyCheckIn") ||
               ContainsRoute(route, "DashboardPage") ||
               ContainsRoute(route, "QuizPage") ||
               ContainsRoute(route, "ProfileSetupPage") ||
               ContainsRoute(route, "Cycle") ||
               ContainsRoute(route, "Workout") ||
               ContainsRoute(route, "Nutrition") ||
               ContainsRoute(route, "Profile") ||
               ContainsRoute(route, "MainTab");
    }

    private static bool ContainsRoute(string route, string segment)
    {
        return route.Contains(segment, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsOnboardingRoute(string route)
    {
        return ContainsRoute(route, nameof(QuizPage)) ||
               ContainsRoute(route, nameof(ProfileSetupPage));
    }

    private static bool IsAuthResetRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;

        return ContainsRoute(route, "Login") || ContainsRoute(route, "Register");
    }
}
