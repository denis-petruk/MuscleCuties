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
public sealed class BarcodeScanCoordinator : IDisposable
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

    /// <summary>Takes the camera service, which may be absent on a device without one, and how long a capture may</summary>
    /// stay open before it is reported as a timeout.</summary>
    public BarcodeScanCoordinator(IBarcodeScannerHardwareService? hardware, TimeSpan? captureTimeout = null)
    {
        _hardware = hardware;
        _captureTimeout = captureTimeout ?? CaptureTimeout;
    }

    /// <summary>True when the device has a camera.</summary>
    public bool HasCamera => _hardware is not null;

    /// <summary>The current scan revision, bumped for each scan.</summary>
    public int Revision => _revision;

    /// <summary>True when the revision is still the current scan.</summary>
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

    /// <summary>
    /// The only place the session token source is cancelled and disposed. The capture deliberately
    /// leaves it in place: a hardware capture that already timed out may still be unwinding on the
    /// captured token, so disposing it there would pull the token out from under that call.
    /// </summary>
    public void Cancel()
    {
        var cancellation = _cancellation;
        _cancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
        _hardware?.CancelActiveCapture();
    }

    /// <summary>Releases the session token source this coordinator owns when its page is torn down.</summary>
    public void Dispose() => Cancel();

    /// <summary>Runs one camera session for the given revision. A session that is no longer current returns as</summary>
    /// cancelled, so a closed panel never receives a late barcode.</summary>
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
    }

    /// <summary>
    /// The shared camera session the nutrition page and the food log page used to copy: the camera
    /// check, the superseding revision, the capture, the timeout, and the decoded-barcode-or-status
    /// decision. The caller keeps its own busy guard, writes the decoded code into its own input
    /// field, and applies a found food to its own surface.
    /// </summary>
    public async Task<BarcodeScanAttempt> ScanAsync(Func<bool> isStillOpen)
    {
        ArgumentNullException.ThrowIfNull(isStillOpen);

        if (!HasCamera)
            return BarcodeScanAttempt.Status(UiTextCatalog.Get(StatusCameraUnavailableKey));

        var sessionRevision = BeginSession();
        var attempt = await CaptureAsync(sessionRevision).ConfigureAwait(true);
        if (!IsCurrent(sessionRevision) || !isStillOpen())
            return BarcodeScanAttempt.Superseded;

        if (attempt.TimedOut)
            return BarcodeScanAttempt.Status(TimeoutStatusText);

        var capture = attempt.Capture;
        return HasUsableBarcode(capture.BarcodeText, capture.Outcome)
            ? new BarcodeScanAttempt(capture.BarcodeText!.Trim(), capture.Symbology, sessionRevision, null)
            : BarcodeScanAttempt.Status(StatusTextFor(capture.Outcome));
    }

    /// <summary>The line shown when the camera produced no code before the timeout.</summary>
    public static string TimeoutStatusText => UiTextCatalog.Get(StatusNoCodeDetectedKey);

    /// <summary>The status line for a capture outcome.</summary>
    public static string StatusTextFor(BarcodeCaptureOutcome outcome) => outcome switch
    {
        BarcodeCaptureOutcome.PermissionDenied => UiTextCatalog.Get(StatusPermissionDeniedKey),
        BarcodeCaptureOutcome.UnsupportedSymbology => UiTextCatalog.Get(StatusUnsupportedSymbologyKey),
        BarcodeCaptureOutcome.Cancelled => UiTextCatalog.Get(StatusScanCancelledKey),
        _ => UiTextCatalog.Get(StatusCameraUnavailableKey)
    };

    /// <summary>True when the capture produced a barcode worth looking up.</summary>
    public static bool HasUsableBarcode(string? barcodeText, BarcodeCaptureOutcome outcome) =>
        outcome == BarcodeCaptureOutcome.Decoded && !string.IsNullOrWhiteSpace(barcodeText);

    /// <summary>True when the food carries the nutrition the log needs.</summary>
    public static bool HasCompleteNutrition(FoodCatalogItemDto food) =>
        food.Calories is not null && food.Protein is not null && food.Carbs is not null && food.Fats is not null;

    /// <summary>
    /// The shared lookup body: the empty-input guard, the catalog call, the supersede check and the
    /// invalid / not-found / incomplete-nutrition statuses. The caller supplies its visibility
    /// predicate, the projection to its own item type, and the two writes it used to repeat around
    /// the await (its busy flag and the status line); it then applies a found item to its surface.
    /// </summary>
    public async Task<BarcodeLookupAttempt<TFood>> LookupAsync<TFood>(
        ILookupBarcodeUseCase? useCase,
        string? requestedBarcode,
        string currentInput,
        BarcodeSymbology symbology,
        int expectedRevision,
        Func<bool> isStillVisible,
        Func<FoodCatalogItemDto, TFood> mapFood,
        Action<bool>? setProcessing = null,
        Action<string>? setStatus = null) where TFood : class
    {
        ArgumentNullException.ThrowIfNull(isStillVisible);
        ArgumentNullException.ThrowIfNull(mapFood);

        var rawInput = string.IsNullOrWhiteSpace(requestedBarcode) ? currentInput : requestedBarcode;
        if (string.IsNullOrWhiteSpace(rawInput))
            return Finish(UiTextCatalog.Get(StatusEnterDigitsKey));

        if (useCase is null)
            return Finish(IsCurrent(expectedRevision) && isStillVisible()
                ? UiTextCatalog.Get(StatusLookupUnavailableKey)
                : null);

        setProcessing?.Invoke(true);
        setStatus?.Invoke(UiTextCatalog.Get(StatusSearchingCatalogKey));
        try
        {
            var result = await useCase.ExecuteAsync(
                new BarcodeLookupRequest(rawInput.Trim(), (BarcodeSymbologyDto)symbology)).ConfigureAwait(true);
            if (!IsCurrent(expectedRevision) || !isStillVisible())
                return new BarcodeLookupAttempt<TFood>(null, null);

            if (!result.IsValidBarcode)
                return Finish(UiTextCatalog.Get(StatusInvalidFormatKey));

            if (!result.IsFound || result.FoodItem is null)
                return Finish(UiTextCatalog.Format(StatusNotFoundFormatKey, result.NormalizedBarcode));

            if (!HasCompleteNutrition(result.FoodItem))
                return Finish(UiTextCatalog.Get(StatusIncompleteNutritionKey));

            return new BarcodeLookupAttempt<TFood>(null, mapFood(result.FoodItem));
        }
        catch (BarcodeLookupUnavailableException)
        {
            return Finish(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch (HttpRequestException)
        {
            return Finish(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch (TaskCanceledException)
        {
            return Finish(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch (OperationCanceledException)
        {
            // The scanner panel was closed or superseded; the newer state owns the status.
            return Finish(null);
        }
        catch (IOException)
        {
            return Finish(UnavailableStatus(expectedRevision, isStillVisible));
        }
        catch
        {
            return Finish(IsCurrent(expectedRevision) && isStillVisible()
                ? UiTextCatalog.Get(StatusLookupFailedKey)
                : null);
        }
        finally
        {
            setProcessing?.Invoke(false);
        }

        BarcodeLookupAttempt<TFood> Finish(string? statusText)
        {
            if (statusText is not null)
                setStatus?.Invoke(statusText);
            return new BarcodeLookupAttempt<TFood>(statusText, null);
        }
    }

    /// <summary>The status shown when the scan is no longer usable.</summary>
    private string? UnavailableStatus(int expectedRevision, Func<bool> isStillVisible) =>
        IsCurrent(expectedRevision) && isStillVisible()
            ? UiTextCatalog.Get(StatusLookupUnavailableKey)
            : null;
}

/// <summary>One camera session's result, with whether the timeout rather than the camera ended it.</summary>
public sealed record BarcodeCaptureAttempt(bool TimedOut, BarcodeCameraCaptureResult Capture);

/// <summary>
/// The outcome of one camera session: either the decoded barcode (with the revision that owns it) or
/// the status line to show. <see cref="BarcodeScanAttempt.Superseded"/> means a newer session or a
/// closed panel already owns the surface and the caller should write nothing.
/// </summary>
public sealed record BarcodeScanAttempt(
    string? BarcodeText,
    BarcodeSymbology Symbology,
    int Revision,
    string? StatusText)
{
    /// <summary>A result for a session a newer one has replaced; the caller must write nothing.</summary>
    public static BarcodeScanAttempt Superseded => new(null, BarcodeSymbology.Auto, 0, null);

    /// <summary>An attempt that only carries a status line.</summary>
    public static BarcodeScanAttempt Status(string statusText) => new(null, BarcodeSymbology.Auto, 0, statusText);
}

/// <summary>One lookup's result: either the food the code resolved to, or the line explaining why not.</summary>
public sealed record BarcodeLookupAttempt<TFood>(string? StatusText, TFood? Food) where TFood : class
{
    /// <summary>True when the lookup found a food.</summary>
    public bool HasFood => Food is not null;
}
