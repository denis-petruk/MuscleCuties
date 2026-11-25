namespace MuscleCuties.UI.ViewModels.Auth;

/// <summary>
///     Reusable coordinator for OTP and password recovery resend countdowns using TimeProvider.
///     The caller supplies the two pieces of button copy so the countdown itself stays free of
///     localization and can be exercised without the UI catalog.
/// </summary>
public sealed class ResendCountdownCoordinator : IDisposable
{
    private readonly Func<int, string> _formatCountdown;
    private readonly Action<int, bool, string> _onStateChanged;
    private readonly string _readyText;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;
    private CancellationTokenSource? _timerCts;

    /// <summary>Takes the state callback and the countdown texts.</summary>
    public ResendCountdownCoordinator(
        Action<int, bool, string> onStateChanged,
        string readyText,
        Func<int, string> formatCountdown,
        TimeProvider? timeProvider = null)
    {
        _onStateChanged = onStateChanged ?? throw new ArgumentNullException(nameof(onStateChanged));
        _readyText = readyText ?? throw new ArgumentNullException(nameof(readyText));
        _formatCountdown = formatCountdown ?? throw new ArgumentNullException(nameof(formatCountdown));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Stops the running countdown.</summary>
    public void Dispose()
    {
        _disposed = true;
        StopCountdown();
    }

    /// <summary>Restarts the resend countdown, cancelling one already running so two timers cannot both decrement it.</summary>
    public void StartCountdown(int seconds)
    {
        StopCountdown();
        if (_disposed) return;

        var initialSeconds = Math.Max(0, seconds);
        var canResend = initialSeconds == 0;
        var buttonText = canResend ? _readyText : _formatCountdown(initialSeconds);
        _onStateChanged(initialSeconds, canResend, buttonText);

        if (canResend) return;

        _timerCts = new CancellationTokenSource();
        var timer = _timerCts;
        _ = RunCountdownAsync(timer, initialSeconds, _timeProvider.GetTimestamp());
    }

    /// <summary>Cancels the running countdown, which is what stops a page that is closing from being ticked later.</summary>
    public void StopCountdown()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
    }

    /// <summary>Ticks the countdown down from a start timestamp rather than counting wake-ups, so a suspended app resumes at</summary>
    /// the right remaining time; it stops as soon as this timer was replaced or cancelled.</summary>
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
                var buttonText = remaining > 0 ? _formatCountdown(remaining) : _readyText;
                _onStateChanged(remaining, canResend, buttonText);

                if (canResend)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
