using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class PhotoJourneyVaultPage : ContentPage
{
    public const int PhotoUnavailableOverlayRow = 2;
    public const int ComparisonSelectionRow = 3;
    public const int ComparisonSecondColumnIndex = 1;
    public const int ComparisonOverlayPageRow = 1;
    public const double ComparisonSelectionColumnSpacing = 8d;
    public const double ComparisonOverlayMaximumWidth = 640d;
    public const double ComparisonContentSpacing = 12d;
    public const double ComparisonImageHeight = 280d;
    public const double ComparisonImageColumnSpacing = 2d;
    public const double EmptyStateMinimumHeight = 220d;
    public static Thickness ComparisonOverlayMargin => new(20d);
    public static Thickness ComparisonOverlayPadding => new(16d);
    private const double MinimumComparisonFraction = 0.1d;
    private const double MaximumComparisonFraction = 0.9d;
    private const double InitialComparisonFraction = 0.5d;
    private const double WholeComparisonWidth = 1d;
    private readonly PhotoJourneyVaultViewModel _viewModel;

    public PhotoJourneyVaultPage(PhotoJourneyVaultViewModel vm)
    {
        _viewModel = this.InitializeAndBind(InitializeComponent, vm);
        ComparisonSlider.Minimum = MinimumComparisonFraction;
        ComparisonSlider.Maximum = MaximumComparisonFraction;
        ComparisonSlider.Value = InitialComparisonFraction;
        UpdateComparisonDivider(InitialComparisonFraction);
    }

    private void OnComparisonSliderChanged(object? sender, ValueChangedEventArgs args)
    {
        UpdateComparisonDivider(args.NewValue);
    }

    private void UpdateComparisonDivider(double fraction)
    {
        var clamped = Math.Clamp(fraction, MinimumComparisonFraction, MaximumComparisonFraction);
        ComparisonFirstColumn.Width = new GridLength(clamped, GridUnitType.Star);
        ComparisonSecondColumn.Width = new GridLength(WholeComparisonWidth - clamped, GridUnitType.Star);
    }

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

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.ResetPrivacy();
        base.OnNavigatedFrom(args);
    }

    private async void OnAddPhotoClicked(object? sender, EventArgs e)
    {
        string? rawPath = null;
        try
        {
            rawPath = await ProfileImagePicker.PickAndStoreAsync("vault", true);
            if (string.IsNullOrWhiteSpace(rawPath))
                return;

            // Prompt optional weight entry
            var weightString = await DisplayPromptAsync(
                UiTextCatalog.Translate("Progress Photo"),
                UiTextCatalog.Translate("Enter your current weight (optional, kg):"),
                UiTextCatalog.Translate("Next"),
                UiTextCatalog.Translate("Skip"),
                keyboard: Keyboard.Numeric);

            double? weightKg = null;
            if (double.TryParse(weightString, out var parsedWeight) && parsedWeight > 0)
                weightKg = parsedWeight;

            // Prompt optional notes
            var notes = await DisplayPromptAsync(
                UiTextCatalog.Translate("Progress Photo"),
                UiTextCatalog.Translate("Add an optional note:"),
                UiTextCatalog.Translate("Save"),
                UiTextCatalog.Translate("Skip"));

            if (!await _viewModel.AddPhotoAsync(rawPath, DateTime.Today, weightKg, notes ?? string.Empty))
                throw new InvalidOperationException("The photo was not saved.");
            if (_viewModel.IsLoadError)
                await DisplayAlertAsync(UiTextCatalog.Translate("Photo Vault"),
                    UiTextCatalog.Translate("The photo was saved, but the vault could not refresh. Reopen it to view the photo."),
                    "OK");
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(rawPath))
                try { File.Delete(rawPath); }
                catch
                {
                    await DisplayAlertAsync(UiTextCatalog.Translate("Photo Vault"),
                        UiTextCatalog.Translate(
                            "The photo could not be saved, and its local copy could not be removed. Please try again."),
                        "OK");
                    return;
                }

            await DisplayAlertAsync(UiTextCatalog.Translate("Photo Vault"),
                UiTextCatalog.Translate("Could not add the photo right now. Please try again."), "OK");
        }
    }
}
