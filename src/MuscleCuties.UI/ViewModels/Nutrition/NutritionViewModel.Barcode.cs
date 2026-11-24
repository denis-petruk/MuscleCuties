using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private readonly ILookupBarcodeUseCase? _barcodeUseCase;
    private readonly IBarcodeScannerHardwareService? _barcodeHardwareService;
    private BarcodeScanCoordinator? _barcodeScan;

    [ObservableProperty] private string _barcodeInputText = string.Empty;
    [ObservableProperty] private string _barcodeScanStatus = string.Empty;
    [ObservableProperty] private bool _isProcessingBarcode;

    private BarcodeScanCoordinator BarcodeScan =>
        _barcodeScan ??= new BarcodeScanCoordinator(_barcodeHardwareService);

    private bool IsBarcodeScannerOpen => IsBarcodeSourceVisible && IsMealEditorVisible;

    [RelayCommand]
    private async Task ScanBarcodeWithCameraAsync()
    {
        if (IsBusy || IsProcessingBarcode || !IsBarcodeScannerOpen)
            return;

        if (!BarcodeScan.HasCamera)
        {
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeScanCoordinator.StatusCameraUnavailableKey);
            return;
        }

        var sessionRevision = BarcodeScan.BeginSession();
        var attempt = await BarcodeScan.CaptureAsync(sessionRevision);
        if (!BarcodeScan.IsCurrent(sessionRevision) || !IsBarcodeScannerOpen)
            return;

        if (attempt.TimedOut)
        {
            BarcodeScanStatus = BarcodeScanCoordinator.TimeoutStatusText;
            return;
        }

        var capture = attempt.Capture;
        if (BarcodeScanCoordinator.HasUsableBarcode(capture.BarcodeText, capture.Outcome))
        {
            var barcodeText = capture.BarcodeText!.Trim();
            BarcodeInputText = barcodeText;
            await LookupBarcodeInternalAsync(barcodeText, capture.Symbology, sessionRevision);
            return;
        }

        BarcodeScanStatus = BarcodeScanCoordinator.StatusTextFor(capture.Outcome);
    }

    [RelayCommand]
    private async Task LookupBarcodeAsync(string? barcodeString)
    {
        var sessionRevision = BarcodeScan.NextRevision();
        await LookupBarcodeInternalAsync(barcodeString, BarcodeSymbology.Auto, sessionRevision);
    }

    private async Task LookupBarcodeInternalAsync(
        string? barcodeString,
        BarcodeSymbology symbology,
        int expectedSessionRevision)
    {
        if (IsBusy || IsProcessingBarcode || !IsBarcodeScannerOpen)
            return;

        if (_barcodeUseCase is null)
        {
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeScanCoordinator.StatusLookupUnavailableKey);
            return;
        }

        IsProcessingBarcode = true;
        BarcodeScanStatus = UiTextCatalog.Get(BarcodeScanCoordinator.StatusSearchingCatalogKey);

        try
        {
            var attempt = await BarcodeScan.LookupAsync(
                _barcodeUseCase,
                barcodeString,
                BarcodeInputText,
                symbology,
                expectedSessionRevision,
                () => IsBarcodeScannerOpen,
                NutritionPresentationMapper.FromCatalogItem);

            if (attempt.Food is { } food)
            {
                FoodSearchResults = [];
                SelectFoodResult(CreateFoodSearchResultItem(food));
                AddFoodMessage = UiTextCatalog.Format(
                    BarcodeScanCoordinator.StatusFoundInCatalogFormatKey, food.Name,
                    Math.Round(food.Calories!.Value));
            }
            else if (attempt.StatusText is { } statusText)
            {
                BarcodeScanStatus = statusText;
            }
        }
        finally
        {
            IsProcessingBarcode = false;
        }
    }
}
