using System.Windows.Input;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfilePrivacyPage : ContentPage
{
    public const string PrivacyPolicyUrl = "https://musclecuties.app/privacy";

    public ICommand BackCommand { get; }

    public ProfilePrivacyPage()
    {
        BackCommand = new Command(async () => await NavigateBackAsync());
        BindingContext = this;
        this.InitializeWithTiming(InitializeComponent);
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

    public static async Task OpenPrivacyPolicyAsync()
    {
        var isRussian = LocalizationService.ActiveLanguage == LocalizationService.RussianLanguageCode;
        if (Uri.TryCreate(PrivacyPolicyUrl, UriKind.Absolute, out var uri) &&
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
                UiTextCatalog.Get("Profile_Privacy_UnavailableTitle"),
                UiTextCatalog.Get("Profile_Privacy_UnavailableMessage"),
                UiTextCatalog.Get("Profile_Privacy_Ok"));
    }
}
