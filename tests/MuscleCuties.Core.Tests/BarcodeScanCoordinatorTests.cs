using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class BarcodeScanCoordinatorTests
{
    [Theory]
    [InlineData(BarcodeCaptureOutcome.PermissionDenied, BarcodeScanCoordinator.StatusPermissionDeniedKey)]
    [InlineData(BarcodeCaptureOutcome.UnsupportedSymbology, BarcodeScanCoordinator.StatusUnsupportedSymbologyKey)]
    [InlineData(BarcodeCaptureOutcome.Cancelled, BarcodeScanCoordinator.StatusScanCancelledKey)]
    [InlineData(BarcodeCaptureOutcome.CameraUnavailable, BarcodeScanCoordinator.StatusCameraUnavailableKey)]
    [InlineData(BarcodeCaptureOutcome.Decoded, BarcodeScanCoordinator.StatusCameraUnavailableKey)]
    public void CaptureOutcomeMapsToTheSameStatusBothPagesShowed(BarcodeCaptureOutcome outcome, string expectedKey)
    {
        Assert.Equal(expectedKey, BarcodeScanCoordinator.StatusTextFor(outcome));
    }

    [Fact]
    public void TimeoutUsesNoCodeDetected() =>
        Assert.Equal(BarcodeScanCoordinator.StatusNoCodeDetectedKey, BarcodeScanCoordinator.TimeoutStatusText);

    [Theory]
    [InlineData(BarcodeCaptureOutcome.Decoded, "0123456789012", true)]
    [InlineData(BarcodeCaptureOutcome.Decoded, "   ", false)]
    [InlineData(BarcodeCaptureOutcome.Decoded, null, false)]
    [InlineData(BarcodeCaptureOutcome.Cancelled, "0123456789012", false)]
    public void OnlyADecodedNonBlankCaptureIsUsable(BarcodeCaptureOutcome outcome, string? text, bool expected)
    {
        Assert.Equal(expected, BarcodeScanCoordinator.HasUsableBarcode(text, outcome));
    }

    [Fact]
    public async Task ACaptureThatExceedsTheTimeoutReportsTimedOut()
    {
        var coordinator = new BarcodeScanCoordinator(new NeverCompletingHardware(), TimeSpan.FromMilliseconds(20));
        var revision = coordinator.BeginSession();

        var attempt = await coordinator.CaptureAsync(revision);

        Assert.True(attempt.TimedOut);
    }

    [Fact]
    public async Task AFailingCaptureReportsCameraUnavailableWithoutTimingOut()
    {
        var coordinator = new BarcodeScanCoordinator(new ThrowingHardware());
        var revision = coordinator.BeginSession();

        var attempt = await coordinator.CaptureAsync(revision);

        Assert.False(attempt.TimedOut);
        Assert.Equal(BarcodeCaptureOutcome.CameraUnavailable, attempt.Capture.Outcome);
    }

    [Fact]
    public async Task StartingASessionSupersedesThePreviousRevision()
    {
        var coordinator = new BarcodeScanCoordinator(new NeverCompletingHardware());
        var first = coordinator.BeginSession();
        var second = coordinator.BeginSession();

        Assert.NotEqual(first, second);
        Assert.False(coordinator.IsCurrent(first));
        Assert.True(coordinator.IsCurrent(second));
        await Task.CompletedTask;
    }

    [Fact]
    public void CancellingStopsTheActiveCameraCapture()
    {
        var hardware = new NeverCompletingHardware();
        var coordinator = new BarcodeScanCoordinator(hardware);
        coordinator.BeginSession();
        var afterBegin = hardware.CancelCalls;

        coordinator.Cancel();

        Assert.Equal(afterBegin + 1, hardware.CancelCalls);
    }

    [Fact]
    public async Task AnEmptyBarcodeAsksForDigits()
    {
        var attempt = await Lookup(new StaticUseCase(Valid(IsFound: true)), "  ", string.Empty);

        Assert.Equal(BarcodeScanCoordinator.StatusEnterDigitsKey, attempt.StatusText);
        Assert.False(attempt.HasFood);
    }

    [Fact]
    public async Task AnInvalidBarcodeReportsTheFormatStatus()
    {
        var attempt = await Lookup(new StaticUseCase(new BarcodeLookupResult(false, false, string.Empty, null, null)), "x");

        Assert.Equal(BarcodeScanCoordinator.StatusInvalidFormatKey, attempt.StatusText);
    }

    [Fact]
    public async Task AnUnknownBarcodeReportsNotFoundWithTheNormalisedCode()
    {
        var attempt = await Lookup(new StaticUseCase(new BarcodeLookupResult(false, true, "0123456789012", null, null)), "0123456789012");

        Assert.Equal(BarcodeScanCoordinator.StatusNotFoundFormatKey, attempt.StatusText);
    }

    [Fact]
    public async Task AFoodWithMissingMacrosIsReportedAsIncomplete()
    {
        var incomplete = Valid(IsFound: true) with { FoodItem = Item(calories: null) };
        var attempt = await Lookup(new StaticUseCase(incomplete), "0123456789012");

        Assert.Equal(BarcodeScanCoordinator.StatusIncompleteNutritionKey, attempt.StatusText);
        Assert.False(attempt.HasFood);
    }

    [Fact]
    public async Task ACompleteFoodIsHandedBackForThePageToApply()
    {
        var attempt = await Lookup(new StaticUseCase(Valid(IsFound: true)), "0123456789012");

        Assert.True(attempt.HasFood);
        Assert.Null(attempt.StatusText);
        Assert.Equal("Porridge", attempt.Food!.Name);
    }

    [Fact]
    public async Task ATransportFailureReportsLookupUnavailableWhileThePageIsVisible()
    {
        var attempt = await Lookup(new ThrowingUseCase(new HttpRequestException()), "0123456789012");

        Assert.Equal(BarcodeScanCoordinator.StatusLookupUnavailableKey, attempt.StatusText);
    }

    [Fact]
    public async Task ASupersededLookupReportsNothingBecauseTheNewerSessionOwnsTheStatus()
    {
        var coordinator = new BarcodeScanCoordinator(new NeverCompletingHardware());
        var staleRevision = coordinator.BeginSession();
        coordinator.NextRevision();

        var attempt = await coordinator.LookupAsync(
            new StaticUseCase(Valid(IsFound: true)), "0123456789012", string.Empty,
            BarcodeSymbology.Auto, staleRevision, () => true, food => food);

        Assert.Null(attempt.StatusText);
        Assert.False(attempt.HasFood);
    }

    private static async Task<BarcodeLookupAttempt<FoodCatalogItemDto>> Lookup(
        ILookupBarcodeUseCase useCase, string input, string? current = null)
    {
        var coordinator = new BarcodeScanCoordinator(new NeverCompletingHardware());
        var revision = coordinator.BeginSession();
        return await coordinator.LookupAsync(
            useCase, input, current ?? string.Empty, BarcodeSymbology.Auto, revision, () => true, food => food);
    }

    private static BarcodeLookupResult Valid(bool IsFound) =>
        new(IsFound, true, "0123456789012", Item(calories: 120f), null);

    private static FoodCatalogItemDto Item(float? calories) =>
        new(1, "Porridge", calories, 5f, 20f, 3f, 100f, "g", null, "Test", "Test", false, "porridge");

    private sealed class StaticUseCase(BarcodeLookupResult result) : ILookupBarcodeUseCase
    {
        public Task<BarcodeLookupResult> ExecuteAsync(BarcodeLookupRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingUseCase(Exception exception) : ILookupBarcodeUseCase
    {
        public Task<BarcodeLookupResult> ExecuteAsync(BarcodeLookupRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<BarcodeLookupResult>(exception);
    }

    private class NeverCompletingHardware : IBarcodeScannerHardwareService
    {
        public int CancelCalls { get; private set; }

        public Task<bool> HasCameraPermissionAsync() => Task.FromResult(true);
        public Task<bool> RequestCameraPermissionAsync() => Task.FromResult(true);

        public virtual async Task<BarcodeCameraCaptureResult> CaptureBarcodeAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.Cancelled);
        }

        public void CancelActiveCapture() => CancelCalls++;
    }

    private sealed class ThrowingHardware : NeverCompletingHardware
    {
        public override Task<BarcodeCameraCaptureResult> CaptureBarcodeAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<BarcodeCameraCaptureResult>(new InvalidOperationException("camera failed"));
    }
}
