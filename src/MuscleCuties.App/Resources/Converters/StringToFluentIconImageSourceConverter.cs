using System.Globalization;
using MauiIcons.Core;
using MauiIcons.Fluent;

namespace MuscleCuties.App.Resources.Converters;

public sealed class StringToFluentIconImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var icon = value is string glyphName && Enum.TryParse<FluentIcons>(glyphName, out var parsedIcon)
            ? parsedIcon
            : FluentIcons.QuestionCircle24;

        var color = ResolveColor(parameter);
        return icon.ToImageSource(color, 24d, false);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Color ResolveColor(object? parameter)
    {
        var key = parameter as string;
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        var resourceKey = theme == AppTheme.Dark ? "TextPrimaryDark" : "TextPrimary";

        if (string.Equals(key, "Warning", StringComparison.OrdinalIgnoreCase))
            return theme == AppTheme.Dark ? Color.FromArgb("#E0A345") : Color.FromArgb("#C77700");

        return Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true && value is Color color
            ? color
            : Colors.Black;
    }
}
