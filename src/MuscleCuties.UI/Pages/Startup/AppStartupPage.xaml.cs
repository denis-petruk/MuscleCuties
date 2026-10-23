using Microsoft.Maui.Controls;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Pages.Startup;

public partial class AppStartupPage : ContentPage
{
    private bool _isThemeHandlerAttached;

    public AppStartupPage()
    {
        this.InitializeWithTiming(InitializeComponent);
        ApplyTheme();
    }

    internal void ShowStartupError(bool unableToUnlockData)
    {
        StartupErrorLabel.Text = unableToUnlockData ? "Unable to Unlock Data" : "Unable to Start";
        StartupRecoveryLabel.Text = unableToUnlockData
            ? "Your data is still on this device. Try again to unlock it, or reset the local database to start fresh."
            : "Something went wrong while starting MuscleCuties. Please retry.";
        StartupActivityIndicator.IsVisible = false;
        StartupActivityIndicator.IsRunning = false;
        StartupStatusLabel.IsVisible = false;
        StartupErrorLabel.IsVisible = true;
        StartupRecoveryLabel.IsVisible = true;
        RetryButton.IsVisible = true;
        ResetButton.IsVisible = unableToUnlockData;
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        StartupErrorLabel.IsVisible = false;
        StartupRecoveryLabel.IsVisible = false;
        RetryButton.IsVisible = false;
        ResetButton.IsVisible = false;
        StartupStatusLabel.Text = "Starting MuscleCuties...";
        StartupStatusLabel.IsVisible = true;
        StartupActivityIndicator.IsVisible = true;
        StartupActivityIndicator.IsRunning = true;
        if (Application.Current is App app)
            try
            {
                await app.RetryStartupAsync();
            }
            catch
            {
                ShowStartupError(false);
            }
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            "Reset Local Database?",
            "If your local data cannot be unlocked due to a key or schema error, resetting will recreate a clean database and repopulate starter data. All existing local records on this device will be erased.",
            "Reset & Repopulate",
            "Cancel");

        if (!confirmed)
            return;

        StartupErrorLabel.IsVisible = false;
        StartupRecoveryLabel.IsVisible = false;
        RetryButton.IsVisible = false;
        ResetButton.IsVisible = false;
        StartupStatusLabel.Text = "Reinitializing database...";
        StartupStatusLabel.IsVisible = true;
        StartupActivityIndicator.IsVisible = true;
        StartupActivityIndicator.IsRunning = true;

        if (Application.Current is App app)
            try
            {
                await app.ResetAndRestartAsync();
            }
            catch
            {
                ShowStartupError(false);
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
