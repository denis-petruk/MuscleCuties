using Microsoft.Maui.Accessibility;
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

    private void OnBirthDatePickerFocused(object? sender, FocusEventArgs e)
    {
        ((ProfileSetupViewModel)BindingContext).MarkBirthDatePickerFocused();
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

        // Missing answers are collected and reported by the view model, each beside its own control.
        if (BindingContext is not ProfileSetupViewModel viewModel)
            return;

        await viewModel.ContinueCommand.ExecuteAsync(null);

        Element? firstErrorTarget = !string.IsNullOrWhiteSpace(viewModel.BirthDateError)
            ? ProfileSetupBirthDateErrorLabel
            : !string.IsNullOrWhiteSpace(viewModel.HeightError)
                ? ProfileSetupHeightErrorLabel
                : !string.IsNullOrWhiteSpace(viewModel.WeightError)
                    ? ProfileSetupWeightErrorLabel
                    : !string.IsNullOrWhiteSpace(viewModel.StrengthActivityError)
                        ? ProfileSetupStrengthActivityErrorLabel
                        : null;

        if (firstErrorTarget is not null)
            await ProfileSetupScroll.ScrollToAsync(firstErrorTarget, ScrollToPosition.Center, true);
    }

    private void OnWellnessLabelTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is ProfileSetupViewModel { IsBusy: true })
            return;

        WellnessAcknowledgment.IsChecked = !WellnessAcknowledgment.IsChecked;
    }

    private async Task ShowProfileImageActionsAsync()
    {
        if (BindingContext is not ProfileSetupViewModel viewModel || viewModel.IsBusy)
            return;

        var choosePhoto = UiTextCatalog.Get("Onboarding_ProfileSetup_ChooseFromPhotos");
        var useIcon = UiTextCatalog.TranslateRuntimeText("Use MuscleCuties icon");
        var options = viewModel.HasProfileImage
            ? new[] { choosePhoto, useIcon }
            : new[] { choosePhoto };

        var action = await DisplayActionSheetAsync(
            UiTextCatalog.Get("Onboarding_ProfileSetup_ProfilePhoto"),
            UiTextCatalog.Get("Common_Cancel"),
            null,
            options);

        if (action == choosePhoto)
            await PickProfileImageAsync(viewModel);
        else if (action == useIcon)
            viewModel.SetProfileImage(null);
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
            await DisplayAlertAsync(
                UiTextCatalog.Get("Onboarding_ProfileSetup_ProfilePhoto"),
                UiTextCatalog.TranslateRuntimeText("Could not change the image on this device right now."),
                UiTextCatalog.Get("Common_Close"));
        }
    }
}
