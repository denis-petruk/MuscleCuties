namespace MuscleCuties.App.Pages.Profile;

public partial class ProfilePrivacyPage : ContentPage
{
    public ProfilePrivacyPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..", true);
    }
}
