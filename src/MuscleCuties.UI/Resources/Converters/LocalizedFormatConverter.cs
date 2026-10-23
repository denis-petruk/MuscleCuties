using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Resources.Converters;

public sealed class LocalizedFormatConverter : IValueConverter, IMarkupExtension<IValueConverter>
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var format = UiTextCatalog.Get(parameter as string ?? string.Empty);
        var displayValue = value is string text ? UiTextCatalog.Translate(text) : value;
        return string.Format(culture, format, displayValue);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Localized formatted text cannot be written to source state.");
    }

    public IValueConverter ProvideValue(IServiceProvider serviceProvider) => this;

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => this;
}
