using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Resources.Converters;

public sealed class LocalizedStringConverter : IValueConverter, IMarkupExtension<IValueConverter>
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            string text when UiTextCatalog.HasKey(text) => UiTextCatalog.Get(text),
            string text => UiTextCatalog.Translate(text),
            Enum option => UiTextCatalog.HasKey(option.ToString())
                ? UiTextCatalog.Get(option.ToString())
                : UiTextCatalog.Translate(option.ToString()),
            _ => value
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Localized display text cannot be written to source state.");
    }

    public IValueConverter ProvideValue(IServiceProvider serviceProvider) => this;

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => this;
}
