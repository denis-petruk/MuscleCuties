using MuscleCuties.App.Pages;
using MuscleCuties.App.Services.Profile;
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

    private async void OnProfileImageTapped(object? sender, TappedEventArgs e)
    {
        var action = await DisplayActionSheetAsync(
            "Profile image",
            "Cancel",
            null,
            "Choose photo",
            "Use MuscleCuties icon");

        var viewModel = (ProfileViewModel)BindingContext;
        switch (action)
        {
            case "Choose photo":
                await PickProfileImageAsync(viewModel);
                break;
            case "Use MuscleCuties icon":
                await viewModel.UpdateProfileImageAsync(null);
                break;
        }
    }

    private async Task PickProfileImageAsync(ProfileViewModel viewModel)
    {
        try
        {
            var imagePath = await ProfileImageFilePicker.PickAndStoreAsync();
            if (!string.IsNullOrWhiteSpace(imagePath))
                await viewModel.UpdateProfileImageAsync(imagePath);
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Profile image", "Could not change the image on this device right now.", "OK");
        }
    }
}
