using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.App.Pages.Auth;
using MuscleCuties.App.Pages.Cycle;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Profile;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.App;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;
    private bool _isThemeHandlerAttached;
    private bool _isResettingProfileRoute;
    private bool _isRedirectingFromGuard;

    public AppShell(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        AttachThemeHandler();
        ApplyTabIcons(ResolveTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified));

        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(QuizPage), typeof(QuizPage));
        Routing.RegisterRoute(nameof(ProfileSetupPage), typeof(ProfileSetupPage));
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
        if (_isRedirectingFromGuard || !RequiresAuthenticatedUser(targetRoute))
            return;

        var deferral = args.GetDeferral();
        _ = GuardAuthenticatedNavigationAsync(args, deferral, targetRoute!);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        ApplyTabIcons(ResolveTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified));
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        if (_isResettingProfileRoute ||
            !IsTabSwitch(args.Source) ||
            CurrentItem?.CurrentItem?.Route != "YouTab")
        {
            return;
        }

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
        void Apply() => ApplyTabIcons(ResolveTheme(e.RequestedTheme));

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

    private static bool IsTabSwitch(ShellNavigationSource source) =>
        source is ShellNavigationSource.ShellItemChanged or
            ShellNavigationSource.ShellSectionChanged or
            ShellNavigationSource.ShellContentChanged;

    private async Task GuardAuthenticatedNavigationAsync(
        ShellNavigatingEventArgs args,
        ShellNavigatingDeferral deferral,
        string targetRoute)
    {
        string? redirectRoute = null;
        try
        {
            using var scope = _services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var userId = await authService.GetCurrentUserIdAsync();
            if (userId <= 0)
            {
                args.Cancel();
                redirectRoute = "//LoginPage";
            }
            else
            {
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await userRepository.GetByIdAsync(userId);
                if (user is null)
                {
                    await authService.LogoutAsync();
                    args.Cancel();
                    redirectRoute = "//LoginPage";
                }
                else if (!user.IsOnboardingComplete && !AllowsOnboardingRoute(targetRoute))
                {
                    args.Cancel();
                    redirectRoute = nameof(QuizPage);
                }
            }
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
            await MainThread.InvokeOnMainThreadAsync(() => GoToAsync(redirectRoute, false));
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

        return ContainsRoute(route, "DashboardPage") ||
               ContainsRoute(route, "Cycle") ||
               ContainsRoute(route, "Workout") ||
               ContainsRoute(route, "Nutrition") ||
               ContainsRoute(route, "Profile") ||
               ContainsRoute(route, "MainTab");
    }

    private static bool ContainsRoute(string route, string segment) =>
        route.Contains(segment, StringComparison.OrdinalIgnoreCase);

    private static bool AllowsOnboardingRoute(string route) =>
        ContainsRoute(route, nameof(ProfileSetupPage));
}
