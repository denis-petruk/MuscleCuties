using Microsoft.Maui.Controls.Shapes;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Parses path data into a geometry, cached because the strings are constants and cells are</summary>
/// recycled.</summary>
public class StringToGeometryConverter : IValueConverter
{
    // PathGeometryConverter is stateless — one shared instance is safe.
    private static readonly PathGeometryConverter _converter = new();

    // Path data strings are constant in this app; cache parsed results for reuse
    // across CollectionView/BindableLayout recycled cells.
    private static readonly Dictionary<string, Geometry?> _cache = new(StringComparer.Ordinal);

    /// <summary>Geometry for the path data. Malformed data yields null so the icon is blank rather than the page</summary>
    /// throwing.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string pathData || string.IsNullOrWhiteSpace(pathData))
            return null;

        if (_cache.TryGetValue(pathData, out var cached))
            return cached;

        Geometry? geometry;
        try
        {
            geometry = _converter.ConvertFromInvariantString(pathData) as Geometry;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            // A malformed path constant must leave the icon blank rather than crash the page.
            geometry = null;
        }

        _cache[pathData] = geometry;
        return geometry;
    }

    /// <summary>Not supported: a geometry cannot become path data.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
