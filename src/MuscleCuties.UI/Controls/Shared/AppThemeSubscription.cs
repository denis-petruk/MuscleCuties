namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Owns the app theme subscription a control needs when it re-renders on a theme change. Attach and detach are
///     idempotent so a control never has to track whether it is currently subscribed.
/// </summary>
public sealed class AppThemeSubscription(Action onThemeChanged)
{
    private bool _isSubscribed;

    public void Attach()
    {
        if (_isSubscribed || Application.Current is not { } app)
            return;

        app.RequestedThemeChanged += HandleThemeChanged;
        _isSubscribed = true;
    }

    public void Detach()
    {
        if (!_isSubscribed || Application.Current is not { } app)
            return;

        app.RequestedThemeChanged -= HandleThemeChanged;
        _isSubscribed = false;
    }

    private void HandleThemeChanged(object? sender, AppThemeChangedEventArgs e) => onThemeChanged();
}
