using System.Diagnostics;
using Microsoft.Maui.Accessibility;
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
        ((ProfileSetupViewModel)BindingContext).MarkBirthDateSelected(e.OldDate, e.NewDate);
    }

    private void OnBirthDatePickerUnfocused(object? sender, FocusEventArgs e)
    {
        ((ProfileSetupViewModel)BindingContext).MarkBirthDateConfirmedFromPicker();
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
            // The message is static text, so announce it explicitly when it appears.
            if (!string.IsNullOrWhiteSpace(AcknowledgmentError.Text))
                SemanticScreenReader.Default.Announce(AcknowledgmentError.Text);
            return;
        }

        AcknowledgmentError.IsVisible = false;

        if (BindingContext is not ProfileSetupViewModel viewModel)
            return;

        await viewModel.ContinueCommand.ExecuteAsync(null);
    }

    private async Task ShowProfileImageActionsAsync()
    {
        var choosePhoto = UiTextCatalog.TranslateRuntimeText("Choose photo");
        var useIcon = UiTextCatalog.TranslateRuntimeText("Use MuscleCuties icon");
        var action = await DisplayActionSheetAsync(
            UiTextCatalog.TranslateRuntimeText("Profile photo"),
            UiTextCatalog.TranslateRuntimeText("Cancel"),
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
            await DisplayAlertAsync(UiTextCatalog.TranslateRuntimeText("Profile photo"),
                UiTextCatalog.TranslateRuntimeText("Could not change the image on this device right now."), "OK");
        }
    }
}
