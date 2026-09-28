using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace MuscleCuties.App.Resources.Converters;

public class StringToGeometryConverter : IValueConverter
{
    // PathGeometryConverter is stateless — one shared instance is safe.
    private static readonly PathGeometryConverter _converter = new();

    // Path data strings are constant in this app; cache parsed results for reuse
    // across CollectionView/BindableLayout recycled cells.
    private static readonly Dictionary<string, Geometry?> _cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string pathData || string.IsNullOrWhiteSpace(pathData))
            return null;

        if (_cache.TryGetValue(pathData, out var cached))
            return cached;

        var geometry = _converter.ConvertFromInvariantString(pathData) as Geometry;
        _cache[pathData] = geometry;
        return geometry;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
