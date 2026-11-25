// Barcode scanning and catalog lookup.
// Second half of NutritionViewModel; see its declaration for the full contract.

using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Barcode entry for the food finder: the typed code and the camera scan, both resolving to a catalog food.</summary>
public partial class NutritionViewModel
{
    private readonly ILookupBarcodeUseCase? _barcodeUseCase;
    private readonly IBarcodeScannerHardwareService? _barcodeHardwareService;
    private BarcodeScanCoordinator? _barcodeScan;

    [ObservableProperty] private string _barcodeInputText = string.Empty;
    [ObservableProperty] private string _barcodeScanStatus = string.Empty;
    [ObservableProperty] private bool _isProcessingBarcode;

    /// <summary>The scan coordinator, created on first use so a page that never scans never touches the camera.</summary>
    private BarcodeScanCoordinator BarcodeScan =>
        _barcodeScan ??= new BarcodeScanCoordinator(_barcodeHardwareService);

    /// <summary>True when the scanner sheet is open.</summary>
    private bool IsBarcodeScannerOpen => IsBarcodeSourceVisible && IsMealEditorVisible;

    /// <summary>Starts the camera scan.</summary>
    [RelayCommand]
    private async Task ScanBarcodeWithCameraAsync()
    {
        if (IsBusy || IsProcessingBarcode || !IsBarcodeScannerOpen)
            return;

        var scan = await BarcodeScan.ScanAsync(() => IsBarcodeScannerOpen);
        if (scan.BarcodeText is { } barcodeText)
        {
            BarcodeInputText = barcodeText;
            await LookupBarcodeInternalAsync(barcodeText, scan.Symbology, scan.Revision);
            return;
        }

        if (scan.StatusText is { } statusText)
            BarcodeScanStatus = statusText;
    }

    /// <summary>Looks up the scanned barcode.</summary>
    [RelayCommand]
    private async Task LookupBarcodeAsync(string? barcodeString)
    {
        var sessionRevision = BarcodeScan.NextRevision();
        await LookupBarcodeInternalAsync(barcodeString, BarcodeSymbology.Auto, sessionRevision);
    }

    /// <summary>Looks the barcode up and shows the food it finds.</summary>
    private async Task LookupBarcodeInternalAsync(
        string? barcodeString,
        BarcodeSymbology symbology,
        int expectedSessionRevision)
    {
        if (IsBusy || IsProcessingBarcode || !IsBarcodeScannerOpen)
            return;

        var attempt = await BarcodeScan.LookupAsync(
            _barcodeUseCase,
            barcodeString,
            BarcodeInputText,
            symbology,
            expectedSessionRevision,
            () => IsBarcodeScannerOpen,
            NutritionPresentationMapper.FromCatalogItem,
            setProcessing: isProcessing => IsProcessingBarcode = isProcessing,
            setStatus: statusText => BarcodeScanStatus = statusText);

        if (attempt.Food is not { } food)
            return;

        FoodSearchResults = [];
        SelectFoodResult(CreateFoodSearchResultItem(food));
        AddFoodMessage = UiTextCatalog.Format(
            BarcodeScanCoordinator.StatusFoundInCatalogFormatKey, food.Name,
            Math.Round(food.Calories!.Value));
    }
}
