using System.Diagnostics;
using System.Globalization;
using MauiIcons.Core;
using MauiIcons.Fluent;
using MuscleCuties.App.Resources.Styles;

namespace MuscleCuties.App.Resources.Converters;

public sealed class StringToFluentIconImageSourceConverter : IValueConverter
{
    // Cache key: (glyphName, parameterString, isDark)
    // Cleared on theme change by the static Application.RequestedThemeChanged handler below.
    private static readonly Dictionary<(string, string, bool), ImageSource> _cache = new();

    static StringToFluentIconImageSourceConverter()
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += (_, _) => _cache.Clear();
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var glyphName = value as string ?? string.Empty;
        var paramKey = parameter as string ?? string.Empty;
        var isDark = (Application.Current?.RequestedTheme ?? AppTheme.Unspecified) == AppTheme.Dark;
        var cacheKey = (glyphName, paramKey, isDark);

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var icon = ResolveIcon(glyphName);
        var color = ResolveColor(paramKey, isDark);
        var source = icon.ToImageSource(color, 24d);

        _cache[cacheKey] = source;
        return source;
    }

    private static FluentIcons ResolveIcon(string glyphName)
    {
        if (!string.IsNullOrEmpty(glyphName) && Enum.TryParse<FluentIcons>(glyphName, out var parsedIcon))
            return parsedIcon;

        if (!string.IsNullOrWhiteSpace(glyphName))
            Trace.WriteLine("[Icons] Unknown Fluent icon.");

        return FluentIcons.QuestionCircle24;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Color ResolveColor(string? key, bool isDark)
    {
        var resourceKey = isDark ? "TextPrimaryDark" : "TextPrimary";

        if (string.Equals(key, "White", StringComparison.OrdinalIgnoreCase))
            return Colors.White;

        if (string.Equals(key, "Accent", StringComparison.OrdinalIgnoreCase))
            return AppThemeResources.GetColor("Primary", "SecondaryDarkText", Colors.Black);

        if (string.Equals(key, "Warning", StringComparison.OrdinalIgnoreCase))
            return AppThemeResources.GetColor("WarningAccentLight", "WarningAccentDark", Colors.Black);

        return AppThemeResources.GetColor(resourceKey, Colors.Black) ?? Colors.Black;
    }
}
