namespace MuscleCuties.Infrastructure.ApiClient;

/// <summary>
/// U-01: raised when the server rejects the refresh token and the client clears the session, so the
/// app shell can return the person to sign-in instead of leaving screens on synthetic 401s.
/// </summary>
public interface IBackendSessionLostSignal
{
    /// <summary>Raised once when a refresh is refused, not on every failed call afterwards.</summary>
    event EventHandler? SessionLost;

    /// <summary>Raises <see cref="SessionLost"/> at most once until <see cref="Acknowledge"/>.</summary>
    void NotifySessionLost();

    /// <summary>The shell handled the loss (sign-in is showing); a later loss may be signalled again.</summary>
    void Acknowledge();
}

/// <summary>Carries the one event that tells the app its session has gone, so every caller stops retrying with a token that</summary>
/// will not come back.</summary>
public sealed class BackendSessionLostSignal : IBackendSessionLostSignal
{
    private const int Idle = 0;
    private const int Raised = 1;
    private int _state = Idle;

    /// <summary>Raised once when a refresh is refused, not on every failed call afterwards.</summary>
    public event EventHandler? SessionLost;

    /// <summary>Announces the loss to whoever is listening, at most once per signal.</summary>
    public void NotifySessionLost()
    {
        if (Interlocked.Exchange(ref _state, Raised) == Idle)
            SessionLost?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears the signal once the app has handled the lost session.</summary>
    public void Acknowledge() => Interlocked.Exchange(ref _state, Idle);
}
