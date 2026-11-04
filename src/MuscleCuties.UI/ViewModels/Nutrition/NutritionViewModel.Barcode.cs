using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private const int BarcodeCameraCaptureTimeoutSeconds = 20;
    private const string BarcodeStatusEnterDigitsKey = "Nutrition_Barcode_StatusEnterDigits";
    private const string BarcodeStatusSearchingCatalogKey = "Nutrition_Barcode_StatusSearchingCatalog";
    private const string BarcodeStatusFoundInCatalogFormatKey = "Nutrition_Barcode_StatusFoundInCatalogFormat";
    private const string BarcodeStatusNotFoundFormatKey = "Nutrition_Barcode_StatusNotFoundFormat";
    private const string BarcodeStatusIncompleteNutritionKey = "Nutrition_Barcode_StatusIncompleteNutrition";
    private const string BarcodeStatusInvalidFormatKey = "Nutrition_Barcode_StatusInvalidFormat";
    private const string BarcodeStatusPermissionDeniedKey = "Nutrition_Barcode_StatusPermissionDenied";
    private const string BarcodeStatusCameraUnavailableKey = "Nutrition_Barcode_StatusCameraUnavailable";
    private const string BarcodeStatusUnsupportedSymbologyKey = "Nutrition_Barcode_StatusUnsupportedSymbology";
    private const string BarcodeStatusNoCodeDetectedKey = "Nutrition_Barcode_StatusNoCodeDetected";
    private const string BarcodeStatusScanCancelledKey = "Nutrition_Barcode_StatusScanCancelled";
    private const string BarcodeStatusLookupUnavailableKey = "Nutrition_Barcode_StatusLookupUnavailable";
    private const string BarcodeStatusLookupFailedKey = "Nutrition_Barcode_StatusLookupFailed";
    private static readonly TimeSpan BarcodeCameraCaptureTimeout =
        TimeSpan.FromSeconds(BarcodeCameraCaptureTimeoutSeconds);

    private readonly ILookupBarcodeUseCase? _barcodeUseCase;
    private readonly IBarcodeScannerHardwareService? _barcodeHardwareService;
    private CancellationTokenSource? _barcodeCameraCancellation;
    private int _barcodeScannerSessionRevision;

    [ObservableProperty] private string _barcodeInputText = string.Empty;
    [ObservableProperty] private string _barcodeScanStatus = string.Empty;
    [ObservableProperty] private bool _isProcessingBarcode;

    [RelayCommand]
    private async Task ScanBarcodeWithCameraAsync()
    {
        if (IsBusy || IsProcessingBarcode || !IsBarcodeSourceVisible || !IsMealEditorVisible)
            return;

        if (_barcodeHardwareService is null)
        {
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusCameraUnavailableKey);
            return;
        }

        CancelBarcodeCameraCapture();
        var sessionRevision = ++_barcodeScannerSessionRevision;
        using var cts = new CancellationTokenSource();
        _barcodeCameraCancellation = cts;
        var captureTimedOut = false;

        try
        {
            var captureTask = _barcodeHardwareService.CaptureBarcodeAsync(cts.Token);
            var completed = await Task.WhenAny(captureTask, Task.Delay(BarcodeCameraCaptureTimeout, cts.Token))
                .ConfigureAwait(true);
            if (!ReferenceEquals(completed, captureTask) && !captureTask.IsCompleted)
            {
                captureTimedOut = true;
                cts.Cancel();
            }

            var capture = await captureTask;
            if (sessionRevision != _barcodeScannerSessionRevision ||
                !IsBarcodeSourceVisible ||
                !IsMealEditorVisible)
                return;

            if (captureTimedOut)
            {
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusNoCodeDetectedKey);
                return;
            }

            switch (capture.Outcome)
            {
                case BarcodeCaptureOutcome.Decoded when !string.IsNullOrWhiteSpace(capture.BarcodeText):
                    BarcodeInputText = capture.BarcodeText.Trim();
                    await LookupBarcodeInternalAsync(capture.BarcodeText.Trim(), capture.Symbology, sessionRevision);
                    break;
                case BarcodeCaptureOutcome.PermissionDenied:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusPermissionDeniedKey);
                    break;
                case BarcodeCaptureOutcome.UnsupportedSymbology:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusUnsupportedSymbologyKey);
                    break;
                case BarcodeCaptureOutcome.Cancelled:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusScanCancelledKey);
                    break;
                default:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusCameraUnavailableKey);
                    break;
            }
        }
        catch (Exception)
        {
            if (sessionRevision == _barcodeScannerSessionRevision)
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusCameraUnavailableKey);
        }
        finally
        {
            if (ReferenceEquals(_barcodeCameraCancellation, cts))
                _barcodeCameraCancellation = null;
        }
    }

    [RelayCommand]
    private async Task LookupBarcodeAsync(string? barcodeString)
    {
        var sessionRevision = ++_barcodeScannerSessionRevision;
        await LookupBarcodeInternalAsync(barcodeString, BarcodeSymbology.Auto, sessionRevision);
    }

    private async Task LookupBarcodeInternalAsync(
        string? barcodeString,
        BarcodeSymbology symbology,
        int expectedSessionRevision)
    {
        if (IsBusy || IsProcessingBarcode || !IsBarcodeSourceVisible || !IsMealEditorVisible)
            return;

        if (_barcodeUseCase is null)
        {
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusLookupUnavailableKey);
            return;
        }

        var rawInput = string.IsNullOrWhiteSpace(barcodeString) ? BarcodeInputText : barcodeString;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusEnterDigitsKey);
            return;
        }

        IsProcessingBarcode = true;
        BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusSearchingCatalogKey);

        try
        {
            var result = await NutritionPresentationMapper.LookupBarcodeAsync(
                _barcodeUseCase, rawInput.Trim(), symbology);
            if (expectedSessionRevision != _barcodeScannerSessionRevision ||
                !IsBarcodeSourceVisible ||
                !IsMealEditorVisible)
                return;

            if (!result.IsValidBarcode)
            {
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusInvalidFormatKey);
                return;
            }

            if (!result.IsFound || result.FoodItem is null)
            {
                BarcodeScanStatus = UiTextCatalog.Format(
                    BarcodeStatusNotFoundFormatKey, result.NormalizedBarcode);
                return;
            }

            var food = result.FoodItem;
            if (food.Calories is null || food.Protein is null || food.Carbs is null || food.Fats is null)
            {
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusIncompleteNutritionKey);
                return;
            }

            FoodSearchResults = [];
            SelectFoodResult(CreateFoodSearchResultItem(food));
            AddFoodMessage = UiTextCatalog.Format(
                BarcodeStatusFoundInCatalogFormatKey, food.Name,
                Math.Round(food.Calories.Value));
        }
        catch (BarcodeLookupUnavailableException)
        {
            ReportUnavailableLookup(expectedSessionRevision);
        }
        catch (HttpRequestException)
        {
            ReportUnavailableLookup(expectedSessionRevision);
        }
        catch (TaskCanceledException)
        {
            ReportUnavailableLookup(expectedSessionRevision);
        }
        catch (OperationCanceledException)
        {
            // The source tab or modal changed; the newer state owns the status.
        }
        catch (IOException)
        {
            ReportUnavailableLookup(expectedSessionRevision);
        }
        catch
        {
            if (expectedSessionRevision == _barcodeScannerSessionRevision && IsBarcodeSourceVisible)
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusLookupFailedKey);
        }
        finally
        {
            IsProcessingBarcode = false;
        }
    }

    private void ReportUnavailableLookup(int expectedSessionRevision)
    {
        if (expectedSessionRevision == _barcodeScannerSessionRevision && IsBarcodeSourceVisible)
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusLookupUnavailableKey);
    }

    private void CancelBarcodeCameraCapture()
    {
        _barcodeCameraCancellation?.Cancel();
        _barcodeCameraCancellation = null;
        _barcodeHardwareService?.CancelActiveCapture();
    }
}