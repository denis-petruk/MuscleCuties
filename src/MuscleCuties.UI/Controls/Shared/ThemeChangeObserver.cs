using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Controls.Shared;

public sealed class ThemeChangeObserver
{
    private readonly Action<AppTheme> _onThemeChanged;
    private bool _isAttached;

    public ThemeChangeObserver(Action<AppTheme> onThemeChanged)
    {
        _onThemeChanged = onThemeChanged;
    }

    public void Attach()
    {
        if (_isAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged += HandleRequestedThemeChanged;
        _isAttached = true;
    }

    public void Detach()
    {
        if (!_isAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged -= HandleRequestedThemeChanged;
        _isAttached = false;
    }

    private void HandleRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _onThemeChanged(e.RequestedTheme);
    }
}
