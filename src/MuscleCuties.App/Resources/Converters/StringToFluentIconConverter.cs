using System.Globalization;
using MauiIcons.Fluent;

namespace MuscleCuties.App.Resources.Converters;

public class StringToFluentIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string glyphName && Enum.TryParse<FluentIcons>(glyphName, out var icon))
            return icon;

        return FluentIcons.QuestionCircle24;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
