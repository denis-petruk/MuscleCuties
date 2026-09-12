using System.Globalization;
using MauiIcons.Core;
using MauiIcons.Fluent;
using MuscleCuties.App.Resources.Styles;

namespace MuscleCuties.App.Resources.Converters;

public sealed class StringToFluentIconImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var icon = value is string glyphName && Enum.TryParse<FluentIcons>(glyphName, out var parsedIcon)
            ? parsedIcon
            : FluentIcons.QuestionCircle24;

        var color = ResolveColor(parameter);
        return icon.ToImageSource(color, 24d);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Color ResolveColor(object? parameter)
    {
        var key = parameter as string;
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        var resourceKey = theme == AppTheme.Dark ? "TextPrimaryDark" : "TextPrimary";

        if (string.Equals(key, "Warning", StringComparison.OrdinalIgnoreCase))
            return AppThemeResources.GetColor("WarningAccentLight", "WarningAccentDark", Colors.Black);

        return AppThemeResources.GetColor(resourceKey, Colors.Black) ?? Colors.Black;
    }
}
