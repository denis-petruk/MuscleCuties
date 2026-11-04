using MuscleCuties.UI.Models.UI.Nutrition;
#if IOS
using AVFoundation;
using CoreFoundation;
using Foundation;
#endif

namespace MuscleCuties.UI.Services.Nutrition;

public sealed class BarcodeScannerHardwareService : IBarcodeScannerHardwareService
{
    private readonly object _captureGate = new();
    private CancellationTokenSource? _activeCaptureCts;

    public async Task<bool> HasCameraPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>().ConfigureAwait(false);
            return status == PermissionStatus.Granted;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RequestCameraPermissionAsync()
    {
        try
        {
            var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.Camera>).ConfigureAwait(false);
            return status == PermissionStatus.Granted;
        }
        catch
        {
            return false;
        }
    }

    public void CancelActiveCapture()
    {
        lock (_captureGate)
        {
            _activeCaptureCts?.Cancel();
            _activeCaptureCts = null;
        }
    }

    public async Task<BarcodeCameraCaptureResult> CaptureBarcodeAsync(CancellationToken cancellationToken = default)
    {
        CancelActiveCapture();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_captureGate)
        {
            _activeCaptureCts = linkedCts;
        }

        try
        {
            var granted = await HasCameraPermissionAsync().ConfigureAwait(false)
                          || await RequestCameraPermissionAsync().ConfigureAwait(false);
            if (!granted)
                return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.PermissionDenied);

            linkedCts.Token.ThrowIfCancellationRequested();
#if IOS
            return await CaptureOnIosCameraAsync(linkedCts.Token).ConfigureAwait(false);
#else
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable);
#endif
        }
        catch (OperationCanceledException)
        {
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.Cancelled);
        }
        catch (Exception ex)
        {
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable, ErrorMessage: ex.Message);
        }
        finally
        {
            lock (_captureGate)
            {
                if (ReferenceEquals(_activeCaptureCts, linkedCts))
                    _activeCaptureCts = null;
            }
        }
    }

#if IOS
    private static async Task<BarcodeCameraCaptureResult> CaptureOnIosCameraAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<BarcodeCameraCaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new AVCaptureSession();
        var device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
        if (device is null)
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable);

        var input = AVCaptureDeviceInput.FromDevice(device, out var error);
        if (input is null || error is not null || !session.CanAddInput(input))
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable);

        session.AddInput(input);
        using var metadataOutput = new AVCaptureMetadataOutput();
        if (!session.CanAddOutput(metadataOutput))
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.CameraUnavailable);

        session.AddOutput(metadataOutput);
        using var queue = new DispatchQueue("com.musclecuties.barcodescanner");
        using var metadataDelegate = new OneShotMetadataDelegate(session, tcs);
        metadataOutput.SetDelegate(metadataDelegate, queue);

        var supported = metadataOutput.AvailableMetadataObjectTypes;
        var desired = AVMetadataObjectType.EAN13Code
                      | AVMetadataObjectType.EAN8Code
                      | AVMetadataObjectType.UPCECode
                      | AVMetadataObjectType.Code128Code;
        var activeTypes = supported & desired;
        if (activeTypes == 0)
            return new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.UnsupportedSymbology);

        metadataOutput.MetadataObjectTypes = activeTypes;
        using var registration = cancellationToken.Register(() =>
        {
            if (session.Running)
                session.StopRunning();
            tcs.TrySetResult(new BarcodeCameraCaptureResult(BarcodeCaptureOutcome.Cancelled));
        });

        session.StartRunning();
        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            if (session.Running)
                session.StopRunning();
        }
    }

    private sealed class OneShotMetadataDelegate(
        AVCaptureSession session,
        TaskCompletionSource<BarcodeCameraCaptureResult> completion) : AVCaptureMetadataOutputObjectsDelegate
    {
        private int _completed;

        public override void DidOutputMetadataObjects(
            AVCaptureMetadataOutput captureOutput,
            AVMetadataObject[] metadataObjects,
            AVCaptureConnection connection)
        {
            if (metadataObjects is null || metadataObjects.Length == 0)
                return;

            foreach (var obj in metadataObjects)
            {
                if (obj is not AVMetadataMachineReadableCodeObject readable ||
                    string.IsNullOrWhiteSpace(readable.StringValue))
                    continue;

                if (Interlocked.Exchange(ref _completed, 1) != 0)
                    return;

                if (session.Running)
                    session.StopRunning();

                var symbology = readable.Type switch
                {
                    AVMetadataObjectType.EAN13Code => BarcodeSymbology.Ean13,
                    AVMetadataObjectType.EAN8Code => BarcodeSymbology.Ean8,
                    AVMetadataObjectType.UPCECode => BarcodeSymbology.UpcE,
                    AVMetadataObjectType.Code128Code => BarcodeSymbology.Code128,
                    _ => BarcodeSymbology.Auto
                };

                completion.TrySetResult(new BarcodeCameraCaptureResult(
                    BarcodeCaptureOutcome.Decoded,
                    readable.StringValue.Trim(),
                    symbology));
                return;
            }
        }
    }
#endif
}
