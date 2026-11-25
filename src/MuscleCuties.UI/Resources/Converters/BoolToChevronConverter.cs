using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Expanded/collapsed chevron for a disclosure row.</summary>
public class BoolToChevronConverter : IValueConverter
{
    /// <summary>Down-pointing chevron when expanded, right-pointing otherwise.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool expanded && expanded ? "▾" : "▸";
    }

    /// <summary>Not supported: the chevron is output only.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
