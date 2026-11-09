namespace MuscleCuties.Infrastructure.ApiClient;

/// <summary>
/// U-01: raised when the server rejects the refresh token and the client clears the session, so the
/// app shell can return the person to sign-in instead of leaving screens on synthetic 401s.
/// </summary>
public interface IBackendSessionLostSignal
{
    event EventHandler? SessionLost;

    /// <summary>Raises <see cref="SessionLost"/> at most once until <see cref="Acknowledge"/>.</summary>
    void NotifySessionLost();

    /// <summary>The shell handled the loss (sign-in is showing); a later loss may be signalled again.</summary>
    void Acknowledge();
}

public sealed class BackendSessionLostSignal : IBackendSessionLostSignal
{
    private const int Idle = 0;
    private const int Raised = 1;
    private int _state = Idle;

    public event EventHandler? SessionLost;

    public void NotifySessionLost()
    {
        if (Interlocked.Exchange(ref _state, Raised) == Idle)
            SessionLost?.Invoke(this, EventArgs.Empty);
    }

    public void Acknowledge() => Interlocked.Exchange(ref _state, Idle);
}
