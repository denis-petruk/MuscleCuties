using System.Globalization;
using Microsoft.Maui.Controls;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Resources.Converters;

public class CyclePhaseToBrushConverter : IValueConverter
{
    // Cache brushes per (phase, isDark). The 4 phases × 2 themes = 8 entries total.
    private static readonly Dictionary<(CyclePhase, bool), SolidColorBrush> _cache = new();

    static CyclePhaseToBrushConverter()
    {
        AppThemeResources.RegisterCacheClearer(_cache.Clear);
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CyclePhase phase)
            return new SolidColorBrush(Colors.Transparent);

        var isDark = AppThemeResources.IsDark;
        var key = (phase, isDark);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var color = phase switch
        {
            CyclePhase.Menstrual => AppThemeResources.GetColor(
                "CyclePhaseMenstrualLight",
                "CyclePhaseMenstrualDark"),
            CyclePhase.Follicular => AppThemeResources.GetColor(
                "CyclePhaseFollicularLight",
                "CyclePhaseFollicularDark"),
            CyclePhase.Ovulatory => AppThemeResources.GetColor(
                "CyclePhaseOvulatoryLight",
                "CyclePhaseOvulatoryDark"),
            CyclePhase.Luteal => AppThemeResources.GetColor(
                "CyclePhaseLutealLight",
                "CyclePhaseLutealDark"),
            _ => Colors.Transparent
        };

        var brush = new SolidColorBrush(color);
        _cache[key] = brush;
        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(CyclePhaseToBrushConverter)} does not support reverse conversion.");
    }
}
