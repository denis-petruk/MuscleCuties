using MuscleCuties.UI.Controls.Profile;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel vm)
    {
        _viewModel = vm;
        this.InitializeAndBind(InitializeComponent, vm);
    }

    private static string T(string text)
    {
        return UiTextCatalog.Translate(text);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        // Refresh the Apple Health badge after returning from the connection page.
        _viewModel.Invalidate();
        this.BeginPageLoad(async () =>
        {
            await ProfileImagePicker.ProtectExistingAsync();
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
        });
        this.BeginDeferredLoad(() => DetailsLazy.LoadIfNeededAsync(true).AsTask());
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var shouldLogout = await DisplayAlertAsync(
            T("Log out"),
            T("Are you sure you want to log out?"),
            T("Log out"),
            T("Cancel"));

        if (!shouldLogout)
            return;

        await _viewModel.LogoutCommand.ExecuteAsync(null);
    }

    private async void OnDeleteAccountClicked(object? sender, EventArgs e)
    {
        // First confirmation dialog
        var firstConfirm = await DisplayAlertAsync(
            T("Delete Account and Local Data"),
            T(
                "Are you sure you want to delete your account? All workout logs, cycle records, nutrition logs, and progress photos stored on this device will be permanently deleted."),
            T("Continue"),
            T("Cancel"));

        if (!firstConfirm)
            return;

        // Second confirmation dialog
        var finalConfirm = await DisplayAlertAsync(
            T("Final Confirmation"),
            T(
                "This action cannot be undone. It deletes this account's local records and photos. Other accounts on this device remain. You will see an error if cleanup is incomplete."),
            T("Delete this account"),
            T("Cancel"));

        if (!finalConfirm)
            return;

        await _viewModel.DeleteAccountAndDataCommand.ExecuteAsync(null);
    }

    private async void OnProfileImageTapped(object? sender, TappedEventArgs e)
    {
        var choosePhoto = T("Choose photo");
        var useIcon = T("Use MuscleCuties icon");
        var action = await DisplayActionSheetAsync(
            T("Profile image"),
            T("Cancel"),
            null,
            choosePhoto,
            useIcon);

        switch (action)
        {
            case var photoSelection when photoSelection == choosePhoto:
                await PickProfileImageAsync(_viewModel);
                break;
            case var iconSelection when iconSelection == useIcon:
                await _viewModel.UpdateProfileImageAsync(null);
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
            CropModalLazy.InputTransparent = false;
            try
            {
                var croppedPath = await cropModal.ShowAsync(rawPath);
                if (!string.IsNullOrWhiteSpace(croppedPath))
                    try
                    {
                        await viewModel.UpdateProfileImageAsync(croppedPath);
                    }
                    catch
                    {
                        File.Delete(croppedPath);
                        throw;
                    }
            }
            finally
            {
                CropModalLazy.InputTransparent = true;
                File.Delete(rawPath);
            }
        }
        catch
        {
            await DisplayAlertAsync(T("Profile image"), T("Could not change the image on this device right now."),
                "OK");
        }
    }
}
