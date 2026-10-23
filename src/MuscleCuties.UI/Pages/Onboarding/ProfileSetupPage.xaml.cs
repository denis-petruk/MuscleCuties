using System.Diagnostics;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Onboarding;

public partial class ProfileSetupPage : ContentPage
{
    public ProfileSetupPage(ProfileSetupViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        Trace.WriteLine("[Performance][ProfileSetupPage] Constructor entered.");
        this.InitializeWithTiming(InitializeComponent);
        this.BindWithTiming(vm, started);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => ((ProfileSetupViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    private async void OnProfileImageTapped(object? sender, TappedEventArgs e)
    {
        await ShowProfileImageActionsAsync();
    }

    private void OnBirthDateSelected(object? sender, DateChangedEventArgs e)
    {
        ((ProfileSetupViewModel)BindingContext).MarkBirthDateSelected();
    }

    private async void OnProfileImageButtonClicked(object? sender, EventArgs e)
    {
        await ShowProfileImageActionsAsync();
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        Unfocus();

        if (!WellnessAcknowledgment.IsChecked)
        {
            AcknowledgmentError.IsVisible = true;
            return;
        }

        AcknowledgmentError.IsVisible = false;

        if (BindingContext is not ProfileSetupViewModel viewModel)
            return;

        await viewModel.ContinueCommand.ExecuteAsync(null);
    }

    private async Task ShowProfileImageActionsAsync()
    {
        var choosePhoto = UiTextCatalog.Translate("Choose photo");
        var useIcon = UiTextCatalog.Translate("Use MuscleCuties icon");
        var action = await DisplayActionSheetAsync(
            UiTextCatalog.Translate("Profile photo"),
            UiTextCatalog.Translate("Cancel"),
            null,
            choosePhoto,
            useIcon);

        var viewModel = (ProfileSetupViewModel)BindingContext;
        switch (action)
        {
            case var selection when selection == choosePhoto:
                await PickProfileImageAsync(viewModel);
                break;
            case var iconSelection when iconSelection == useIcon:
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
        catch
        {
            await DisplayAlertAsync(UiTextCatalog.Translate("Profile photo"),
                UiTextCatalog.Translate("Could not change the image on this device right now."), "OK");
        }
    }
}
