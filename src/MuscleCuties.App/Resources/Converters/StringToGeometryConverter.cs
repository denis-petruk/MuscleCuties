using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace MuscleCuties.App.Resources.Converters;

public class StringToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string pathData && !string.IsNullOrWhiteSpace(pathData))
        {
            var converter = new PathGeometryConverter();
            return converter.ConvertFromInvariantString(pathData);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
