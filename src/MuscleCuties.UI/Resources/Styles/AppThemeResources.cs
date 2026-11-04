using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Resources.Styles;

public static class AppThemeResources
{
    public static bool IsDark => (Application.Current?.RequestedTheme ?? AppTheme.Unspecified) == AppTheme.Dark;

    public static double GetDouble(string key) => GetRequired<double>(key);

    public static Thickness GetThickness(string key) => GetRequired<Thickness>(key);

    public static string GetString(string key) => GetRequired<string>(key);

    public static T GetRequired<T>(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is T resource)
            return resource;

        throw new InvalidOperationException($"Required app style resource '{key}' is missing or has an unexpected type.");
    }

    public static void RegisterCacheClearer(Action clear)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += (_, _) => clear();
    }

    public static Color GetColor(string lightKey, string darkKey, Color? fallback = null)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        return GetColor(theme, lightKey, darkKey, fallback);
    }

    public static Color GetColor(AppTheme theme, string lightKey, string darkKey, Color? fallback = null)
    {
        var key = theme == AppTheme.Dark ? darkKey : lightKey;
        return GetColor(key, fallback);
    }

    public static Color GetColor(string key, Color? fallback = null)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true)
        {
            if (value is Color color)
                return color;

            if (value is SolidColorBrush brush)
                return brush.Color;
        }

        return fallback ?? GetRequired<Color>("Transparent");
    }
}
