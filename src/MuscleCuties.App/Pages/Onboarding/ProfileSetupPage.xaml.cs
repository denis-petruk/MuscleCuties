using MuscleCuties.App.Pages;
using MuscleCuties.App.Services.Profile;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Onboarding;

public partial class ProfileSetupPage : ContentPage
{
    public ProfileSetupPage(ProfileSetupViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileSetupViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    private async void OnProfileImageTapped(object? sender, TappedEventArgs e)
    {
        await ShowProfileImageActionsAsync();
    }

    private async void OnProfileImageButtonClicked(object? sender, EventArgs e)
    {
        await ShowProfileImageActionsAsync();
    }

    private async Task ShowProfileImageActionsAsync()
    {
        var action = await DisplayActionSheetAsync(
            "Profile photo",
            "Cancel",
            null,
            "Choose photo",
            "Use MuscleCuties icon");

        var viewModel = (ProfileSetupViewModel)BindingContext;
        switch (action)
        {
            case "Choose photo":
                await PickProfileImageAsync(viewModel);
                break;
            case "Use MuscleCuties icon":
                viewModel.SetProfileImage(null);
                break;
        }
    }

    private async Task PickProfileImageAsync(ProfileSetupViewModel viewModel)
    {
        try
        {
            var imagePath = await ProfileImagePicker.PickAndStoreAsync();
            if (!string.IsNullOrWhiteSpace(imagePath))
                viewModel.SetProfileImage(imagePath);
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Profile photo", "Could not change the image on this device right now.", "OK");
        }
    }
}
