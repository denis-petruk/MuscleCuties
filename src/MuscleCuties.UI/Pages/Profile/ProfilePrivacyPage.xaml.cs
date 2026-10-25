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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        if (LocalizationService.ActiveLanguage != "ru")
        {
            HeaderView.Title = "Privacy";
            HeaderView.Subtitle = "Your data and account security";
            SummaryTitle.Text = "Your data, at a glance";
            SummaryBody.Text = "Your account and health records are stored in MuscleCuties services.";
            ServicesTitle.Text = "Other services";
            EmailBody.Text = "Verification emails use our email provider.";
            FoodBody.Text = "Food search may send generic names to USDA, never your logs.";
            HealthBody.Text = "Apple Health steps are read only with your permission.";
            ChoiceTitle.Text = "Your choices";
            ChoiceBody.Text = "You can delete your account and local data from Profile.";
            PolicyButton.Text = "Read privacy policy";
            SemanticProperties.SetDescription(PolicyButton, "Open the full privacy policy");
            return;
        }

        HeaderView.Title = "Конфиденциальность";
        HeaderView.Subtitle = "Безопасность данных и аккаунта";
        SummaryTitle.Text = "Ваши данные — кратко";
        SummaryBody.Text = "Данные аккаунта и здоровья хранятся в сервисах MuscleCuties.";
        ServicesTitle.Text = "Другие сервисы";
        EmailBody.Text = "Письма для подтверждения отправляет наш почтовый провайдер.";
        FoodBody.Text = "При поиске продуктов в USDA могут передаваться общие названия, но не ваши записи.";
        HealthBody.Text = "Шаги из Apple Health считываются только с вашего разрешения.";
        ChoiceTitle.Text = "Ваш выбор";
        ChoiceBody.Text = "Удалить аккаунт и локальные данные можно в профиле.";
        PolicyButton.Text = "Читать политику конфиденциальности";
        SemanticProperties.SetDescription(PolicyButton, "Открыть полную политику конфиденциальности");
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
        var isRussian = LocalizationService.ActiveLanguage == "ru";
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
                isRussian ? "Политика недоступна" : "Privacy policy unavailable",
                isRussian
                    ? "Не удалось открыть политику конфиденциальности. Повторите попытку позже."
                    : "The privacy policy could not be opened. Please try again later.",
                isRussian ? "Хорошо" : "OK");
    }
}
