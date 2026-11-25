using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     Privacy page: data handling, health sync disclosure, and account deletion.
/// </summary>
public partial class ProfilePrivacyPage : ContentPage
{
    public const string PrivacyPolicyUrl = "https://musclecuties.app/privacy";
    public const string TermsOfUseUrl = "https://musclecuties.app/terms";

    /// <summary>The command that returns to the profile.</summary>
    public ICommand BackCommand { get; }
    /// <summary>The health sync settings shown on the page.</summary>
    public ProfileHealthSyncViewModel HealthSync { get; }

    /// <summary>Takes the health-sync state the page displays and creates the back command.</summary>
    public ProfilePrivacyPage(ProfileHealthSyncViewModel healthSync)
    {
        HealthSync = healthSync;
        BackCommand = new AsyncRelayCommand(NavigateBackAsync);
        BindingContext = this;
        this.InitializeWithTiming(InitializeComponent);
    }

    /// <summary>Starts the health sync section when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = HealthSync.RefreshAsync();
    }

    /// <summary>Goes back, doing nothing when there is no shell to navigate.</summary>
    private static async Task NavigateBackAsync()
    {
        if (Shell.Current is not { } shell)
            return;

        try
        {
            if (shell.Navigation?.NavigationStack?.Count > 1)
            {
                await shell.GoToAsync("..");
                return;
            }
            await shell.GoToAsync("..");
        }
        catch
        {
            await shell.GoToAsync("..");
        }
    }

    /// <summary>Opens the privacy policy.</summary>
    private async void OnPrivacyPolicyClicked(object? sender, EventArgs e) =>
        await OpenPrivacyPolicyAsync();

    /// <summary>Opens the terms of use.</summary>
    private async void OnTermsOfUseClicked(object? sender, EventArgs e) =>
        await OpenTermsOfUseAsync();

    /// <summary>Opens the privacy policy in the system browser.</summary>
    public static Task OpenPrivacyPolicyAsync() =>
        OpenPolicyAsync(
            PrivacyPolicyUrl,
            "Profile_Privacy_UnavailableTitle",
            "Profile_Privacy_UnavailableMessage");

    /// <summary>Opens the terms of use in the system browser.</summary>
    public static Task OpenTermsOfUseAsync() =>
        OpenPolicyAsync(
            TermsOfUseUrl,
            "Profile_Privacy_TermsUnavailableTitle",
            "Profile_Privacy_TermsUnavailableMessage");

    /// <summary>Opens a policy in the browser; when no browser can handle the link it explains why in the reader's</summary>
    /// language instead of failing silently.</summary>
    private static async Task OpenPolicyAsync(string url, string unavailableTitleKey, string unavailableMessageKey)
    {
        var isRussian = LocalizationService.ActiveLanguage == LocalizationService.RussianLanguageCode;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo))
        {
            try
            {
                var policyUri = new UriBuilder(uri) { Query = isRussian ? "lang=ru" : "lang=en" }.Uri;
                if (await Launcher.Default.OpenAsync(policyUri))
                    return;
            }
            catch (Exception)
            {
                // Show the same recovery message when the browser cannot open.
            }
        }

        if (Shell.Current is { } shell)
            await shell.DisplayAlertAsync(
                UiTextCatalog.Get(unavailableTitleKey),
                UiTextCatalog.Get(unavailableMessageKey),
                UiTextCatalog.Get("Profile_Privacy_Ok"));
    }
}
