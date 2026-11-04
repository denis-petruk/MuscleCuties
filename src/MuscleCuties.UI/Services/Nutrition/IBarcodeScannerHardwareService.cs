using MuscleCuties.UI.Models.UI.Nutrition;

namespace MuscleCuties.UI.Services.Nutrition;

public interface IBarcodeScannerHardwareService
{
    Task<bool> HasCameraPermissionAsync();
    Task<bool> RequestCameraPermissionAsync();
    Task<BarcodeCameraCaptureResult> CaptureBarcodeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable));
    void CancelActiveCapture() { }
}
