using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>
/// The one barcode capture and lookup decision path. The nutrition page and the food log page each
/// carried their own copy of the camera session (timeout, cancellation, superseded revision) and of
/// the outcome and lookup status tables; both now route through this collaborator, which sits next
/// to the scanner hardware service it drives.
/// </summary>
public sealed class BarcodeScanCoordinator
{
    public const int CaptureTimeoutSeconds = 20;

    public const string StatusEnterDigitsKey = "Nutrition_Barcode_StatusEnterDigits";
    public const string StatusSearchingCatalogKey = "Nutrition_Barcode_StatusSearchingCatalog";
    public const string StatusFoundInCatalogFormatKey = "Nutrition_Barcode_StatusFoundInCatalogFormat";
    public const string StatusNotFoundFormatKey = "Nutrition_Barcode_StatusNotFoundFormat";
    public const string StatusIncompleteNutritionKey = "Nutrition_Barcode_StatusIncompleteNutrition";
    public const string StatusInvalidFormatKey = "Nutrition_Barcode_StatusInvalidFormat";
    public const string StatusPermissionDeniedKey = "Nutrition_Barcode_StatusPermissionDenied";
    public const string StatusCameraUnavailableKey = "Nutrition_Barcode_StatusCameraUnavailable";
    public const string StatusUnsupportedSymbologyKey = "Nutrition_Barcode_StatusUnsupportedSymbology";
    public const string StatusNoCodeDetectedKey = "Nutrition_Barcode_StatusNoCodeDetected";
    public const string StatusScanCancelledKey = "Nutrition_Barcode_StatusScanCancelled";
    public const string StatusLookupUnavailableKey = "Nutrition_Barcode_StatusLookupUnavailable";
    public const string StatusLookupFailedKey = "Nutrition_Barcode_StatusLookupFailed";

    public static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(CaptureTimeoutSeconds);

    private readonly IBarcodeScannerHardwareService? _hardware;
    private readonly TimeSpan _captureTimeout;
    private CancellationTokenSource? _cancellation;
    private int _revision;

    public BarcodeScanCoordinator(IBarcodeScannerHardwareService? hardware, TimeSpan? captureTimeout = null)
    {
        _hardware = hardware;
        _captureTimeout = captureTimeout ?? CaptureTimeout;
    }

    public bool HasCamera => _hardware is not null;

    public int Revision => _revision;

    public bool IsCurrent(int revision) => revision == _revision;

    /// <summary>Supersedes any in-flight capture or lookup without starting a new one.</summary>
    public int NextRevision() => ++_revision;

    /// <summary>Starts a capture session, superseding and cancelling any earlier one.</summary>
    public int BeginSession()
    {
        Cancel();
        _revision++;
        _cancellation = new CancellationTokenSource();
        return _revision;
    }

    public void Cancel()
    {
        _cancellation?.Cancel();
        _cancellation = null;
        _hardware?.CancelActiveCapture();
    }

    public async Task<BarcodeCaptureAttempt> CaptureAsync(int revision)
    {
        var cancellation = _cancellation;
        var hardware = _hardware;
        if (cancellation is null || hardware is null || !IsCurrent(revision))
            return new BarcodeCaptureAttempt(false, new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.Cancelled));

        try
        {
            var captureTask = hardware.CaptureBarcodeAsync(cancellation.Token);
            var completed = await Task.WhenAny(captureTask, Task.Delay(_captureTimeout, cancellation.Token))
                .ConfigureAwait(true);
            if (!ReferenceEquals(completed, captureTask) && !captureTask.IsCompleted)
            {
                cancellation.Cancel();
                return new BarcodeCaptureAttempt(true,
                    new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable));
            }

            return new BarcodeCaptureAttempt(false, await captureTask.ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            return new BarcodeCaptureAttempt(false, new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.Cancelled));
        }
        catch (Exception)
        {
            return new BarcodeCaptureAttempt(false, new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable));
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
                _cancellation = null;
        }
    }

    public static string TimeoutStatusText => UiTextCatalog.Get(StatusNoCodeDetectedKey);

    public static string StatusTextFor(BarcodeCaptureOutcome outcome) => outcome switch
    {
        BarcodeCaptureOutcome.PermissionDenied => UiTextCatalog.Get(StatusPermissionDeniedKey),
        BarcodeCaptureOutcome.UnsupportedSymbology => UiTextCatalog.Get(StatusUnsupportedSymbologyKey),
        BarcodeCaptureOutcome.Cancelled => UiTextCatalog.Get(StatusScanCancelledKey),
        _ => UiTextCatalog.Get(StatusCameraUnavailableKey)
    };

    public static bool HasUsableBarcode(string? barcodeText, BarcodeCaptureOutcome outcome) =>
        outcome == BarcodeCaptureOutcome.Decoded && !string.IsNullOrWhiteSpace(barcodeText);

    public static bool HasCompleteNutrition(FoodCatalogItemDto food) =>
        food.Calories is not null && food.Protein is not null && food.Carbs is not null && food.Fats is not null;

    /// <summary>
    /// The shared lookup body: the empty-input guard, the catalog call, the supersede check and the
    /// invalid / not-found / incomplete-nutrition statuses. The caller supplies its visibility
    /// predicate and the projection to its own item type, then applies a found item to its surface.
    /// </summary>
    public async Task<BarcodeLookupAttempt<TFood>> LookupAsync<TFood>(
        ILookupBarcodeUseCase useCase,
        string? requestedBarcode,
        string currentInput,
        BarcodeSymbology symbology,
        int expectedRevision,
        Func<bool> isStillVisible,
        Func<FoodCatalogItemDto, TFood> mapFood) where TFood : class
    {
        var rawInput = string.IsNullOrWhiteSpace(requestedBarcode) ? currentInput : requestedBarcode;
        if (string.IsNullOrWhiteSpace(rawInput))
            return BarcodeLookupAttempt<TFood>.WithoutFood(UiTextCatalog.Get(StatusEnterDigitsKey));

        try
        {
            var result = await useCase.ExecuteAsync(
                new BarcodeLookupRequest(rawInput.Trim(), (BarcodeSymbologyDto)symbology)).ConfigureAwait(true);
            if (!IsCurrent(expectedRevision) || !isStillVisible())
                return BarcodeLookupAttempt<TFood>.WithoutFood(null);

            if (!result.IsValidBarcode)
                return BarcodeLookupAttempt<TFood>.WithoutFood(UiTextCatalog.Get(StatusInvalidFormatKey));

            if (!result.IsFound || result.FoodItem is null)
            {
                return BarcodeLookupAttempt<TFood>.WithoutFood(
                    UiTextCatalog.Format(StatusNotFoundFormatKey, result.NormalizedBarcode));
            }

            if (!HasCompleteNutrition(result.FoodItem))
                return BarcodeLookupAttempt<TFood>.WithoutFood(UiTextCatalog.Get(StatusIncompleteNutritionKey));

            return new BarcodeLookupAttempt<TFood>(null, mapFood(result.FoodItem));
        }
        catch (BarcodeLookupUnavailableException)
        {
            return BarcodeLookupAttempt<TFood>.WithoutFood(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch (HttpRequestException)
        {
            return BarcodeLookupAttempt<TFood>.WithoutFood(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch (TaskCanceledException)
        {
            return BarcodeLookupAttempt<TFood>.WithoutFood(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch (OperationCanceledException)
        {
            // The scanner panel was closed or superseded; the newer state owns the status.
            return BarcodeLookupAttempt<TFood>.WithoutFood(null);
        }
        catch (IOException)
        {
            return BarcodeLookupAttempt<TFood>.WithoutFood(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch
        {
            return BarcodeLookupAttempt<TFood>.WithoutFood(
                IsCurrent(expectedRevision) && isStillVisible()
                    ? UiTextCatalog.Get(StatusLookupFailedKey)
                    : null);
        }
    }

    private string? UnavailableStatus(int expectedRevision, Func<bool> isStillVisible) =>
        IsCurrent(expectedRevision) && isStillVisible()
            ? UiTextCatalog.Get(StatusLookupUnavailableKey)
            : null;
}

public sealed record BarcodeCaptureAttempt(bool TimedOut, BarcodeCameraCaptureResult Capture);

public sealed record BarcodeLookupAttempt<TFood>(string? StatusText, TFood? Food) where TFood : class
{
    public bool HasFood => Food is not null;

    public static BarcodeLookupAttempt<TFood> WithoutFood(string? statusText) => new(statusText, null);
}
