namespace MuscleCuties.UI.ViewModels.Auth;

/// <summary>
///     Reusable coordinator for OTP and password recovery resend countdowns using TimeProvider.
/// </summary>
public sealed class ResendCountdownCoordinator : IDisposable
{
    private readonly Action<int, bool, string> _onStateChanged;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;
    private CancellationTokenSource? _timerCts;

    public ResendCountdownCoordinator(Action<int, bool, string> onStateChanged, TimeProvider? timeProvider = null)
    {
        _onStateChanged = onStateChanged ?? throw new ArgumentNullException(nameof(onStateChanged));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Dispose()
    {
        _disposed = true;
        StopCountdown();
    }

    public void StartCountdown(int seconds)
    {
        StopCountdown();
        if (_disposed) return;

        var initialSeconds = Math.Max(0, seconds);
        var canResend = initialSeconds == 0;
        var buttonText = canResend ? "Resend code" : $"Resend in {initialSeconds}s";
        _onStateChanged(initialSeconds, canResend, buttonText);

        if (canResend) return;

        _timerCts = new CancellationTokenSource();
        var timer = _timerCts;
        _ = RunCountdownAsync(timer, initialSeconds, _timeProvider.GetTimestamp());
    }

    private async Task RunCountdownAsync(CancellationTokenSource timer, int seconds, long startedAt)
    {
        try
        {
            while (!timer.IsCancellationRequested && !_disposed)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, timer.Token);
                if (_disposed || !ReferenceEquals(_timerCts, timer))
                    return;

                var remaining = Math.Max(0, (int)Math.Ceiling(
                    seconds - _timeProvider.GetElapsedTime(startedAt).TotalSeconds));
                var canResend = remaining == 0;
                var buttonText = remaining > 0 ? $"Resend in {remaining}s" : "Resend code";
                _onStateChanged(remaining, canResend, buttonText);

                if (canResend)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void StopCountdown()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
    }
}
