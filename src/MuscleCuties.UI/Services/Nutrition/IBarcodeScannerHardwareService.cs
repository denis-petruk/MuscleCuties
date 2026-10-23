using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

public interface IBarcodeScannerHardwareService
{
    Task<bool> HasCameraPermissionAsync();
    Task<bool> RequestCameraPermissionAsync();
    Task<BarcodeCameraCaptureResultDto> CaptureBarcodeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new BarcodeCameraCaptureResultDto(BarcodeCaptureOutcomeDto.CameraUnavailable));
    void CancelActiveCapture() { }
}
