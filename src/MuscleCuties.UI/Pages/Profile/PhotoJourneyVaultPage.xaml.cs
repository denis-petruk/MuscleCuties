using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     Photo journey vault: progress photos, side-by-side comparison, and visibility controls.
/// </summary>
public partial class PhotoJourneyVaultPage : ContentPage
{
    public const int PhotoUnavailableOverlayRow = 2;
    public const int ComparisonSelectionRow = 3;
    public const int ComparisonSecondColumnIndex = 1;
    public const int ComparisonOverlayPageRow = 1;
    private readonly PhotoJourneyVaultViewModel _viewModel;
    private bool _isAddingPhoto;

    /// <summary>Initializes the page and its view model.</summary>
    public PhotoJourneyVaultPage(PhotoJourneyVaultViewModel vm)
    {
        _viewModel = this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Starts the vault when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.ResetPrivacy();
        _viewModel.Invalidate();
        this.BeginPageLoad(async () =>
        {
            await ProfileImagePicker.ProtectExistingAsync();
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
        });
    }

    /// <summary>Releases the page when navigated away from.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.ResetPrivacy();
        base.OnNavigatedFrom(args);
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsComparisonOpen)
        {
            _viewModel.CloseComparisonCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>Adds a progress photo, guarding against a second tap while the first is still running.</summary>
    private async void OnAddPhotoClicked(object? sender, EventArgs e)
    {
        if (_isAddingPhoto)
            return;

        _isAddingPhoto = true;
        AddPhotoButton.IsEnabled = false;
        string? rawPath = null;
        try
        {
            rawPath = await ProfileImagePicker.PickAndStoreAsync("vault", true);
            if (string.IsNullOrWhiteSpace(rawPath))
                return;

            // Prompt optional weight entry
            var weightString = await DisplayPromptAsync(
                UiTextCatalog.TranslateRuntimeText("Progress Photo"),
                UiTextCatalog.TranslateRuntimeText("Enter your current weight (optional, kg):"),
                UiTextCatalog.TranslateRuntimeText("Next"),
                UiTextCatalog.TranslateRuntimeText("Skip"),
                keyboard: Keyboard.Numeric);

            double? weightKg = null;
            if (double.TryParse(weightString, out var parsedWeight) && parsedWeight > 0)
                weightKg = parsedWeight;

            // Prompt optional notes
            var notes = await DisplayPromptAsync(
                UiTextCatalog.TranslateRuntimeText("Progress Photo"),
                UiTextCatalog.TranslateRuntimeText("Add an optional note:"),
                UiTextCatalog.TranslateRuntimeText("Save"),
                UiTextCatalog.TranslateRuntimeText("Skip"));

            if (!await _viewModel.AddPhotoAsync(rawPath, DateTime.Today, weightKg, notes ?? string.Empty))
                throw new InvalidOperationException("The photo was not saved.");
            if (_viewModel.IsLoadError)
                await DisplayAlertAsync(
                    UiTextCatalog.Get("Profile_PhotoJourney_Title"),
                    UiTextCatalog.TranslateRuntimeText("The photo was saved, but the vault could not refresh. Reopen it to view the photo."),
                    UiTextCatalog.Get("Profile_Privacy_Ok"));
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(rawPath))
                try { File.Delete(rawPath); }
                catch
                {
                    await DisplayAlertAsync(
                        UiTextCatalog.Get("Profile_PhotoJourney_Title"),
                        UiTextCatalog.TranslateRuntimeText(
                            "The photo could not be saved, and its local copy could not be removed. Please try again."),
                        UiTextCatalog.Get("Profile_Privacy_Ok"));
                    return;
                }

            await DisplayAlertAsync(
                UiTextCatalog.Get("Profile_PhotoJourney_Title"),
                UiTextCatalog.Get("Profile_PhotoVault_AddPhotoFailed"),
                UiTextCatalog.Get("Profile_Privacy_Ok"));
        }
        finally
        {
            _isAddingPhoto = false;
            AddPhotoButton.IsEnabled = true;
        }
    }
}
