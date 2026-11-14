using System.Globalization;
using MuscleCuties.UI.Services.Formatting;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Binds a session date through <see cref="DateText" /> so list and detail share one shape.</summary>
public sealed class SessionDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime date ? DateText.SessionDate(date) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
