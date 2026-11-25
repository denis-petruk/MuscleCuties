using Microsoft.Maui.Accessibility;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Onboarding;

/// <summary>
///     Onboarding step one: birth date, measurements, units, and activity types, before the quiz.
/// </summary>
public partial class ProfileSetupPage : ContentPage
{
    /// <summary>Takes its view model, times its own construction, and binds before presentation.</summary>
    public ProfileSetupPage(ProfileSetupViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        Trace.WriteLine("[Performance][ProfileSetupPage] Constructor entered.");
        this.InitializeWithTiming(InitializeComponent);
        this.BindWithTiming(vm, started);
    }

    /// <summary>Starts the view model's work when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => ((ProfileSetupViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    /// <summary>Offers the photo choices for the avatar.</summary>
    private async void OnProfileImageTapped(object? sender, TappedEventArgs e)
    {
        await ShowProfileImageActionsAsync();
    }

    /// <summary>Tells the view model the date field is in use, so its validation waits until the user is done.</summary>
    private void OnBirthDatePickerFocused(object? sender, FocusEventArgs e)
    {
        ((ProfileSetupViewModel)BindingContext).MarkBirthDatePickerFocused();
    }

    /// <summary>Reports the change so the age rule is applied to the newly picked date.</summary>
    private void OnBirthDateSelected(object? sender, DateChangedEventArgs e)
    {
        ((ProfileSetupViewModel)BindingContext).MarkBirthDateSelected(e.OldDate, e.NewDate);
    }

    /// <summary>Reports that the date came from the picker, which is treated as a confirmed answer.</summary>
    private void OnBirthDatePickerUnfocused(object? sender, FocusEventArgs e)
    {
        ((ProfileSetupViewModel)BindingContext).MarkBirthDateConfirmedFromPicker();
    }

    /// <summary>Offers the photo choices from the avatar button.</summary>
    private async void OnProfileImageButtonClicked(object? sender, EventArgs e)
    {
        await ShowProfileImageActionsAsync();
    }

    /// <summary>Submits the step and scrolls to the first field the view model reported as missing, so the answer to</summary>
    /// fix is on screen.</summary>
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

    /// <summary>Toggles the wellness acknowledgment from its label, enlarging the tap target.</summary>
    private void OnWellnessLabelTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is ProfileSetupViewModel { IsBusy: true })
            return;

        WellnessAcknowledgment.IsChecked = !WellnessAcknowledgment.IsChecked;
    }

    /// <summary>Asks whether to pick a photo or keep the app icon, doing nothing while the form is submitting.</summary>
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

    /// <summary>Picks and stores an avatar, reporting a failure on the page rather than silently keeping the old</summary>
    /// image.</summary>
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
