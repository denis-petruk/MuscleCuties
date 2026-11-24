using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfilePrivacyPage : ContentPage
{
    public const string PrivacyPolicyUrl = "https://musclecuties.app/privacy";
    public const string TermsOfUseUrl = "https://musclecuties.app/terms";

    public ICommand BackCommand { get; }
    public ProfileHealthSyncViewModel HealthSync { get; }

    public ProfilePrivacyPage(ProfileHealthSyncViewModel healthSync)
    {
        HealthSync = healthSync;
        BackCommand = new AsyncRelayCommand(NavigateBackAsync);
        BindingContext = this;
        this.InitializeWithTiming(InitializeComponent);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = HealthSync.RefreshAsync();
    }

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

    private async void OnPrivacyPolicyClicked(object? sender, EventArgs e) =>
        await OpenPrivacyPolicyAsync();

    private async void OnTermsOfUseClicked(object? sender, EventArgs e) =>
        await OpenTermsOfUseAsync();

    public static Task OpenPrivacyPolicyAsync() =>
        OpenPolicyAsync(
            PrivacyPolicyUrl,
            "Profile_Privacy_UnavailableTitle",
            "Profile_Privacy_UnavailableMessage");

    public static Task OpenTermsOfUseAsync() =>
        OpenPolicyAsync(
            TermsOfUseUrl,
            "Profile_Privacy_TermsUnavailableTitle",
            "Profile_Privacy_TermsUnavailableMessage");

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
