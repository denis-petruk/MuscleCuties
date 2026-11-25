using Microsoft.Maui.Controls;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Phase text colour, cached per phase and theme and cleared on a theme change.</summary>
public class CyclePhaseToTextColorConverter : IValueConverter
{
    // Cache colors per (phase, isDark). 4 phases + 1 default × 2 themes = 10 entries max.
    private static readonly Dictionary<(CyclePhase?, bool), Color> _cache = new();

    /// <summary>Registers the phase text colours the converter resolves.</summary>
    static CyclePhaseToTextColorConverter()
    {
        AppThemeResources.RegisterCacheClearer(_cache.Clear);
    }

    /// <summary>Text colour for the phase, or the neutral primary text colour when there is no phase.</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var phase = value is CyclePhase p ? (CyclePhase?)p : null;
        var key = (phase, AppThemeResources.IsDark);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var color = phase is { } known
            ? AppThemeResources.GetCyclePhaseColor(known, textTone: true)
            : AppThemeResources.GetColor("TextPrimary", "TextPrimaryDark");

        _cache[key] = color;
        return color;
    }

    /// <summary>Not supported: a colour cannot become a phase.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException(
            $"{nameof(CyclePhaseToTextColorConverter)} does not support reverse conversion.");
    }
}
