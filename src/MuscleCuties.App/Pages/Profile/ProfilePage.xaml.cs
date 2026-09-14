using MuscleCuties.App.Controls.Profile;
using MuscleCuties.App.Services.Profile;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (BindingContext is null)
            BindingContext = _viewModel;
        this.BeginPageLoad(() => _viewModel.LoadDataCommand.ExecuteAsync(null));
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
            var rawPath = await ProfileImagePicker.PickAndStoreAsync();
            if (string.IsNullOrWhiteSpace(rawPath))
                return;

            await CropModalLazy.LoadIfNeededAsync(true);
            var cropModal = (ProfileImageCropModal)CropModalLazy.Content;
            var croppedPath = await cropModal.ShowAsync(rawPath);
            if (!string.IsNullOrWhiteSpace(croppedPath))
                await viewModel.UpdateProfileImageAsync(croppedPath);
        }
        catch
        {
            await DisplayAlertAsync("Profile image", "Could not change the image on this device right now.", "OK");
        }
    }
}
