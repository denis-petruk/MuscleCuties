
namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>Camera access for barcode capture. The defaults let a device without a camera satisfy the contract:</summary>
/// capture reports the camera as unavailable and cancelling does nothing.</summary>
public interface IBarcodeScannerHardwareService
{
    Task<bool> HasCameraPermissionAsync();
    Task<bool> RequestCameraPermissionAsync();
    Task<BarcodeCameraCaptureResult> CaptureBarcodeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable));
    void CancelActiveCapture() { }
}
