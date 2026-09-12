using MuscleCuties.App.Resources.Styles;

namespace MuscleCuties.App.Pages.Startup;

public partial class AppStartupPage : ContentPage
{
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

        if (Application.Current is App app)
            app.BeginStartup();

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

        var background = AppThemeResources.GetColor(theme, "PageBackground", "PageBackgroundDark");
        BackgroundColor = background;
        StartupRoot.BackgroundColor = background;
        StartupLogo.Source = ImageSource.FromFile(isDark
            ? "musclecuties_logo_dark_transparent.png"
            : "musclecuties_logo_light_transparent.png");
    }
}
