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
    private static readonly TimeSpan BarcodeCameraCaptureTimeout =
        TimeSpan.FromSeconds(BarcodeScanRule.CameraCaptureTimeoutSeconds);

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
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.CameraUnavailableKey);
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
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.NoCodeDetectedKey);
                return;
            }

            switch (capture.Outcome)
            {
                case BarcodeCaptureOutcome.Decoded when !string.IsNullOrWhiteSpace(capture.BarcodeText):
                    BarcodeInputText = capture.BarcodeText.Trim();
                    await LookupBarcodeInternalAsync(capture.BarcodeText.Trim(), capture.Symbology, sessionRevision);
                    break;
                case BarcodeCaptureOutcome.PermissionDenied:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.PermissionDeniedKey);
                    break;
                case BarcodeCaptureOutcome.UnsupportedSymbology:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.UnsupportedSymbologyKey);
                    break;
                case BarcodeCaptureOutcome.Cancelled:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.ScanCancelledKey);
                    break;
                default:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.CameraUnavailableKey);
                    break;
            }
        }
        catch (Exception)
        {
            if (sessionRevision == _barcodeScannerSessionRevision)
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.CameraUnavailableKey);
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
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.LookupUnavailableKey);
            return;
        }

        var rawInput = string.IsNullOrWhiteSpace(barcodeString) ? BarcodeInputText : barcodeString;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.EnterDigitsKey);
            return;
        }

        IsProcessingBarcode = true;
        BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.SearchingCatalogKey);

        try
        {
            var result = await NutritionPresentationMapper.LookupBarcodeAsync(
                _barcodeUseCase, rawInput.Trim(), symbology);
            if (expectedSessionRevision != _barcodeScannerSessionRevision ||
                !IsBarcodeSourceVisible ||
                !IsMealEditorVisible)
                return;

            switch (result.State)
            {
                case BarcodeScanState.EnterDigits:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.EnterDigitsKey);
                    return;
                case BarcodeScanState.InvalidFormat:
                    BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.InvalidFormatKey);
                    return;
                case BarcodeScanState.NotFound:
                    BarcodeScanStatus = UiTextCatalog.Format(
                        BarcodeStatusText.NotFoundFormatKey, result.NormalizedBarcode);
                    return;
            }

            var food = result.FoodItem!;
            if (food.Calories is null || food.Protein is null || food.Carbs is null || food.Fats is null)
            {
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.IncompleteNutritionKey);
                return;
            }

            FoodSearchResults = [];
            SelectFoodResult(CreateFoodSearchResultItem(food));
            AddFoodMessage = UiTextCatalog.Format(
                BarcodeStatusText.FoundInCatalogFormatKey, food.Name,
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
                BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.LookupFailedKey);
        }
        finally
        {
            IsProcessingBarcode = false;
        }
    }

    private void ReportUnavailableLookup(int expectedSessionRevision)
    {
        if (expectedSessionRevision == _barcodeScannerSessionRevision && IsBarcodeSourceVisible)
            BarcodeScanStatus = UiTextCatalog.Get(BarcodeStatusText.LookupUnavailableKey);
    }

    private void CancelBarcodeCameraCapture()
    {
        _barcodeCameraCancellation?.Cancel();
        _barcodeCameraCancellation = null;
        _barcodeHardwareService?.CancelActiveCapture();
    }
}