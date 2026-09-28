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

    internal void ShowStartupError()
    {
        StartupActivityIndicator.IsVisible = false;
        StartupActivityIndicator.IsRunning = false;
        StartupErrorLabel.IsVisible = true;
        RetryButton.IsVisible = true;
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        StartupErrorLabel.IsVisible = false;
        RetryButton.IsVisible = false;
        StartupActivityIndicator.IsVisible = true;
        StartupActivityIndicator.IsRunning = true;
        if (Application.Current is App app)
        {
            try
            {
                await app.RetryStartupAsync();
            }
            catch
            {
                ShowStartupError();
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyTheme();

        if (Application.Current is App app)
            app.BeginStartup(this);

        if (!_isThemeHandlerAttached && Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
            _isThemeHandlerAttached = true;
        }
    }

    protected override void OnDisappearing()
    {
        if (Application.Current is App app)
            app.DetachStartupPage(this);

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
