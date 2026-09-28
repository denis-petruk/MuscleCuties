using System.Globalization;
using MuscleCuties.App.Resources.Styles;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.App.Resources.Converters;

public class CyclePhaseToTextColorConverter : IValueConverter
{
    // Cache colors per (phase, isDark). 4 phases + 1 default × 2 themes = 10 entries max.
    private static readonly Dictionary<(CyclePhase?, bool), Color> _cache = new();

    static CyclePhaseToTextColorConverter()
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += (_, _) => _cache.Clear();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = (Application.Current?.RequestedTheme ?? AppTheme.Unspecified) == AppTheme.Dark;
        var phase = value is CyclePhase p ? (CyclePhase?)p : null;
        var key = (phase, isDark);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var color = phase switch
        {
            CyclePhase.Menstrual => AppThemeResources.GetColor(
                "CyclePhaseMenstrualTextLight",
                "CyclePhaseMenstrualTextDark"),
            CyclePhase.Follicular => AppThemeResources.GetColor(
                "CyclePhaseFollicularTextLight",
                "CyclePhaseFollicularTextDark"),
            CyclePhase.Ovulatory => AppThemeResources.GetColor(
                "CyclePhaseOvulatoryTextLight",
                "CyclePhaseOvulatoryTextDark"),
            CyclePhase.Luteal => AppThemeResources.GetColor(
                "CyclePhaseLutealTextLight",
                "CyclePhaseLutealTextDark"),
            _ => AppThemeResources.GetColor("TextPrimary", "TextPrimaryDark")
        };

        _cache[key] = color;
        return color;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException(
            $"{nameof(CyclePhaseToTextColorConverter)} does not support reverse conversion.");
    }
}
