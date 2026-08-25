using Microsoft.Maui.ApplicationModel;
using MuscleCuties.App.Pages.Auth;
using MuscleCuties.App.Pages.Cycle;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Profile;

namespace MuscleCuties.App;

public partial class AppShell : Shell
{
    private bool _isThemeHandlerAttached;

    public AppShell()
    {
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
        Routing.RegisterRoute(nameof(ProfileUnitsDisplayPage), typeof(ProfileUnitsDisplayPage));
        Routing.RegisterRoute(nameof(ProfileFeedbackPage), typeof(ProfileFeedbackPage));
        Routing.RegisterRoute(nameof(ProfilePrivacyPage), typeof(ProfilePrivacyPage));
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        ApplyTabIcons(ResolveTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified));
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
}
