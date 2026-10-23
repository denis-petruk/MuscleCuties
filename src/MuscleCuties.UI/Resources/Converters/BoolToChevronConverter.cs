using System.Globalization;
using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Resources.Converters;

public class BoolToChevronConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool expanded && expanded ? "▾" : "▸";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
