namespace MuscleCuties.App.Resources.Styles;

public static class AppThemeResources
{
    public static Color GetColor(string lightKey, string darkKey, Color? fallback = null)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        return GetColor(theme, lightKey, darkKey, fallback);
    }

    public static Color GetColor(AppTheme theme, string lightKey, string darkKey, Color? fallback = null)
    {
        var key = theme == AppTheme.Dark ? darkKey : lightKey;

        return GetColor(key, fallback) ?? fallback ?? Colors.Transparent;
    }

    public static Color? GetColor(string key, Color? fallback = null)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) != true)
            return fallback;

        return value switch
        {
            Color color => color,
            SolidColorBrush brush => brush.Color,
            _ => fallback
        };
    }
}
