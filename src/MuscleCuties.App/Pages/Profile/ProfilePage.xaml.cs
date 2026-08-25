using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfilePage : ContentPage
{
    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var shouldLogout = await DisplayAlertAsync(
            "Log out",
            "Are you want to log out?",
            "Log out",
            "Cancel");

        if (!shouldLogout)
            return;

        await ((ProfileViewModel)BindingContext).LogoutCommand.ExecuteAsync(null);
    }
}
