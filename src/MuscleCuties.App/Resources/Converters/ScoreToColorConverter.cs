using System.Globalization;
using MuscleCuties.App.Resources.Styles;

namespace MuscleCuties.App.Resources.Converters;

public class ScoreToColorConverter : IValueConverter
{
    // Only 3 buckets × 2 themes = 6 entries maximum.
    private static Color? _highLight, _highDark, _medLight, _medDark, _lowLight, _lowDark;

    static ScoreToColorConverter()
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += (_, _) => ClearCache();
    }

    private static void ClearCache()
    {
        _highLight = _highDark = _medLight = _medDark = _lowLight = _lowDark = null;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var score = value is int i ? i : 0;
        var isDark = (Application.Current?.RequestedTheme ?? AppTheme.Unspecified) == AppTheme.Dark;

        if (score >= 70)
        {
            if (isDark)
                return _highDark ??= AppThemeResources.GetColor("ReadinessHighDark", Colors.Green);
            return _highLight ??= AppThemeResources.GetColor("ReadinessHighLight", Colors.Green);
        }

        if (score >= 40)
        {
            if (isDark)
                return _medDark ??= AppThemeResources.GetColor("ReadinessMediumDark", Colors.Orange);
            return _medLight ??= AppThemeResources.GetColor("ReadinessMediumLight", Colors.Orange);
        }

        if (isDark)
            return _lowDark ??= AppThemeResources.GetColor("ReadinessLowDark", Colors.Red);
        return _lowLight ??= AppThemeResources.GetColor("ReadinessLowLight", Colors.Red);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{GetType().Name} does not support reverse conversion.");
    }
}
