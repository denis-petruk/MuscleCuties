using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace MuscleCuties.App.Resources.Converters;

public class StringToGeometryConverter : IValueConverter
{
    private static readonly PathGeometryConverter PathConverter = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string pathData && !string.IsNullOrEmpty(pathData))
            return PathConverter.ConvertFromInvariantString(pathData);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
