using System.Globalization;

namespace MuscleCuties.App.Resources.Converters;

public class BoolToChevronConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool expanded && expanded) ? "▾" : "▸";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
