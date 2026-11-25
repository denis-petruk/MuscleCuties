using MuscleCuties.UI.Controls.Profile;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     You tab: the profile summary and the settings rows.
/// </summary>
public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;
    private IDisposable? _profileNameCapSubscription;

    /// <summary>Initializes the page and its view model.</summary>
    public ProfilePage(ProfileViewModel vm)
    {
        _viewModel = vm;
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Starts the view model's work when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _profileNameCapSubscription ??= CappedTextSize.Attach(
            ProfileNameLabel, "UiFontSize18", "PresentationAccessibilityProfileNameMaxFontSize");
    }

    /// <summary>Pauses the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        _profileNameCapSubscription?.Dispose();
        _profileNameCapSubscription = null;
        base.OnDisappearing();
    }

    /// <summary>Translates a runtime string, for the confirmations built on this page rather than in the catalog.</summary>
    private static string T(string text)
    {
        return UiTextCatalog.TranslateRuntimeText(text);
    }

    /// <summary>Refreshes the profile when navigated to.</summary>
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

    /// <summary>Confirms, then signs out.</summary>
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

    /// <summary>Asks twice before deleting the account, then tells the view model to delete it with its data.</summary>
    private async void OnDeleteAccountClicked(object? sender, EventArgs e)
    {
        // First confirmation dialog
        var firstConfirm = await DisplayAlertAsync(
            UiTextCatalog.Get("Profile_Page_DeleteAccountAndLocalData"),
            T(
                "Are you sure you want to delete your account? All workout logs, cycle records, nutrition logs, and progress photos stored in your account and on this device will be permanently deleted."),
            T("Continue"),
            T("Cancel"));

        if (!firstConfirm)
            return;

        // Second confirmation dialog
        var finalConfirm = await DisplayAlertAsync(
            T("Final Confirmation"),
            T(
                "This action cannot be undone. It deletes this account's cloud and local records and photos. Other accounts on this device remain. You will see an error if cleanup is incomplete."),
            T("Delete this account"),
            T("Cancel"));

        if (!finalConfirm)
            return;

        await _viewModel.DeleteAccountAndDataCommand.ExecuteAsync(null);
    }

    /// <summary>Offers a new photo or the app icon, then applies the choice.</summary>
    private async void OnProfileImageTapped(object? sender, EventArgs e)
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

    /// <summary>Picks and stores a new avatar, reporting a failure on the page.</summary>
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
            await DisplayAlertAsync(
                T("Profile image"),
                T("Could not change the image on this device right now."),
                UiTextCatalog.Get("Profile_Privacy_Ok"));
        }
    }
}
