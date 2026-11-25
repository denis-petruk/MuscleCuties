using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Formats a value into a localized pattern in XAML. The parameter names the format string, and a</summary>
/// string value is translated first so a stored label is not inserted untranslated.</summary>
public sealed class LocalizedFormatConverter : IValueConverter, IMarkupExtension<IValueConverter>
{
    /// <summary>Looks up the format, translates a string value, and formats it in the supplied culture.</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var format = UiTextCatalog.Get(parameter as string ?? string.Empty);
        var displayValue = value is string text ? UiTextCatalog.TranslateRuntimeText(text) : value;
        return string.Format(culture, format, displayValue);
    }

    /// <summary>Not supported: the formatted text is display only.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Localized formatted text cannot be written to source state.");
    }

    /// <summary>Returns the converter itself for XAML markup usage.</summary>
    public IValueConverter ProvideValue(IServiceProvider serviceProvider) => this;

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => this;
}
