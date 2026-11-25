using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Subscribes to theme changes and forwards whether the app is now dark.
/// </summary>
public sealed class ThemeChangeObserver
{
    private readonly Action<AppTheme> _onThemeChanged;
    private bool _isAttached;

    /// <summary>Takes the callback to run whenever the app's theme really changes.</summary>
    public ThemeChangeObserver(Action<AppTheme> onThemeChanged)
    {
        _onThemeChanged = onThemeChanged;
    }

    /// <summary>Starts observing the app theme.</summary>
    public void Attach()
    {
        if (_isAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged += HandleRequestedThemeChanged;
        _isAttached = true;
    }

    /// <summary>Stops observing, so a control that has been disposed is never called back.</summary>
    public void Detach()
    {
        if (!_isAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged -= HandleRequestedThemeChanged;
        _isAttached = false;
    }

    /// <summary>Forwards a real theme change and ignores the unspecified value the platform reports before a theme is</summary>
    /// known.</summary>
    private void HandleRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _onThemeChanged(e.RequestedTheme);
    }
}
