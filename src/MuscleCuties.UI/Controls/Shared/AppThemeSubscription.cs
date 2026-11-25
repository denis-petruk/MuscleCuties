namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Owns the app theme subscription a control needs when it re-renders on a theme change. Attach and detach are
///     idempotent so a control never has to track whether it is currently subscribed.
/// </summary>
public sealed class AppThemeSubscription(Action onThemeChanged)
{
    private bool _isSubscribed;

    /// <summary>Starts following the app theme so the owner can repaint when it changes.</summary>
    public void Attach()
    {
        if (_isSubscribed || Application.Current is not { } app)
            return;

        app.RequestedThemeChanged += HandleThemeChanged;
        _isSubscribed = true;
    }

    /// <summary>Stops following the theme, so a control that has left the tree is not repainted into it.</summary>
    public void Detach()
    {
        if (!_isSubscribed || Application.Current is not { } app)
            return;

        app.RequestedThemeChanged -= HandleThemeChanged;
        _isSubscribed = false;
    }

    /// <summary>Runs the subscription's callback when the theme changes.</summary>
    private void HandleThemeChanged(object? sender, AppThemeChangedEventArgs e) => onThemeChanged();
}
