using MuscleCuties.UI.Models.UI.Nutrition;

namespace MuscleCuties.UI.Services.Nutrition;

public sealed record BarcodeCameraCaptureAttempt(BarcodeCameraCaptureResult? Capture, bool TimedOut, bool Failed);

/// <summary>
/// The one place that races camera capture against the timeout. The diary and the fast-logging panel both carried
/// this sequence, including its cancellation and failure handling.
/// </summary>
public static class BarcodeCameraCapture
{
    public const int TimeoutSeconds = 20;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

    public static async Task<BarcodeCameraCaptureAttempt> CaptureAsync(
        IBarcodeScannerHardwareService hardware,
        CancellationTokenSource cancellation)
    {
        var captureTask = hardware.CaptureBarcodeAsync(cancellation.Token);

        var completed = await Task.WhenAny(captureTask, Task.Delay(Timeout, cancellation.Token))
            .ConfigureAwait(true);

        var timedOut = false;
        if (!ReferenceEquals(completed, captureTask) && !captureTask.IsCompleted)
        {
            timedOut = true;
            cancellation.Cancel();
        }

        try
        {
            return new BarcodeCameraCaptureAttempt(await captureTask.ConfigureAwait(true), timedOut, false);
        }
        catch (Exception)
        {
            return new BarcodeCameraCaptureAttempt(null, timedOut, true);
        }
    }
}
