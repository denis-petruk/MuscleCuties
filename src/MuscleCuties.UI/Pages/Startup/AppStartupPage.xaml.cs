using Microsoft.Maui.Controls;
using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Pages.Startup;

public partial class AppStartupPage : ContentPage
{
    private bool _isActionInProgress;
    private bool _isThemeHandlerAttached;

    public AppStartupPage()
    {
        this.InitializeWithTiming(InitializeComponent);
        ApplyTheme();
    }

    internal void ShowStartupError(bool unableToUnlockData)
    {
        StartupErrorLabel.Text = unableToUnlockData ? UiTextCatalog.Get("Dashboard_Startup_UnlockFailedTitle") : UiTextCatalog.Get("Startup_UnableToStart");
        StartupRecoveryLabel.Text = unableToUnlockData
            ? UiTextCatalog.Get("Startup_UnlockRecovery")
            : UiTextCatalog.Get("Startup_StartRecovery");
        StartupActivityIndicator.IsVisible = false;
        StartupActivityIndicator.IsRunning = false;
        StartupStatusLabel.IsVisible = false;
        StartupErrorLabel.IsVisible = true;
        StartupRecoveryLabel.IsVisible = true;
        StartupErrorFooter.IsVisible = true;
        RetryButton.IsVisible = true;
        RetryButton.IsEnabled = true;
        ResetButton.IsVisible = true;
        ResetButton.IsEnabled = true;
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        if (_isActionInProgress)
            return;

        _isActionInProgress = true;
        RetryButton.IsEnabled = false;
        ResetButton.IsEnabled = false;
        StartupErrorFooter.IsVisible = false;
        StartupErrorLabel.IsVisible = false;
        StartupRecoveryLabel.IsVisible = false;
        RetryButton.IsVisible = false;
        ResetButton.IsVisible = false;
        StartupStatusLabel.Text = UiTextCatalog.Get("Startup_Starting");
        StartupStatusLabel.IsVisible = true;
        StartupActivityIndicator.IsVisible = true;
        StartupActivityIndicator.IsRunning = true;
        try
        {
            if (Application.Current is App app)
                await app.RetryStartupAsync();
        }
        catch
        {
            ShowStartupError(false);
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        if (_isActionInProgress)
            return;

        _isActionInProgress = true;
        RetryButton.IsEnabled = false;
        ResetButton.IsEnabled = false;
        try
        {
            var confirmed = await DisplayAlertAsync(
                UiTextCatalog.Get("Startup_ResetConfirmationTitle"),
                UiTextCatalog.Get("Startup_ResetConfirmationBody"),
                UiTextCatalog.Get("Startup_ResetAndRepopulate"),
                UiTextCatalog.Get("Common_Cancel"));

            if (!confirmed)
            {
                RetryButton.IsEnabled = true;
                ResetButton.IsEnabled = true;
                return;
            }

            StartupErrorFooter.IsVisible = false;
            StartupErrorLabel.IsVisible = false;
            StartupRecoveryLabel.IsVisible = false;
            RetryButton.IsVisible = false;
            ResetButton.IsVisible = false;
            StartupStatusLabel.Text = UiTextCatalog.Get("Startup_Reinitializing");
            StartupStatusLabel.IsVisible = true;
            StartupActivityIndicator.IsVisible = true;
            StartupActivityIndicator.IsRunning = true;

            if (Application.Current is App app)
                await app.ResetAndRestartAsync();
        }
        catch
        {
            ShowStartupError(false);
        }
        finally
        {
            _isActionInProgress = false;
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
