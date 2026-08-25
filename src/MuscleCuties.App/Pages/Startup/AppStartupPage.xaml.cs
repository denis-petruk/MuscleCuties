using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MuscleCuties.App.Pages.Startup;

public partial class AppStartupPage : ContentPage
{
    private static readonly Color LightBackground = Color.FromArgb("#FFF8FB");
    private static readonly Color DarkBackground = Color.FromArgb("#2B1D24");
    private bool _isThemeHandlerAttached;

    public AppStartupPage()
    {
        InitializeComponent();
        ApplyTheme();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyTheme();

        if (!_isThemeHandlerAttached && Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
            _isThemeHandlerAttached = true;
        }
    }

    protected override void OnDisappearing()
    {
        if (_isThemeHandlerAttached && Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
            _isThemeHandlerAttached = false;
        }

        base.OnDisappearing();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        ApplyTheme(e.RequestedTheme);
    }

    private void ApplyTheme(AppTheme? requestedTheme = null)
    {
        var theme = requestedTheme ?? Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        if (theme == AppTheme.Unspecified)
            theme = AppInfo.RequestedTheme;

        var isDark = theme == AppTheme.Dark;

        var background = isDark ? DarkBackground : LightBackground;
        BackgroundColor = background;
        StartupRoot.BackgroundColor = background;
        StartupLogo.Source = ImageSource.FromFile(isDark
            ? "musclecuties_logo_dark_transparent.png"
            : "musclecuties_logo_light_transparent.png");
    }
}
