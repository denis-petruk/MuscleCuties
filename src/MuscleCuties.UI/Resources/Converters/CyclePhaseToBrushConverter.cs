using Microsoft.Maui.Controls;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Phase fill as a brush, cached per phase and theme and cleared when the theme changes.</summary>
public class CyclePhaseToBrushConverter : IValueConverter
{
    // Cache brushes per (phase, isDark). The 4 phases × 2 themes = 8 entries total.
    private static readonly Dictionary<(CyclePhase, bool), SolidColorBrush> _cache = new();

    /// <summary>Registers the phase brushes the converter resolves.</summary>
    static CyclePhaseToBrushConverter()
    {
        AppThemeResources.RegisterCacheClearer(_cache.Clear);
    }

    /// <summary>Brush for the phase; a value that is not a phase paints transparent rather than failing.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CyclePhase phase)
            return new SolidColorBrush(AppThemeResources.GetRequired<Color>("Transparent"));

        var key = (phase, AppThemeResources.IsDark);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var brush = new SolidColorBrush(AppThemeResources.GetCyclePhaseColor(phase));
        _cache[key] = brush;
        return brush;
    }

    /// <summary>Not supported: a brush cannot become a phase.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(CyclePhaseToBrushConverter)} does not support reverse conversion.");
    }
}
