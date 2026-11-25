using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Translates a resource key or a stored English string in XAML, so a binding can carry copy without</summary>
/// the view model resolving it.</summary>
public sealed class LocalizedStringConverter : IValueConverter, IMarkupExtension<IValueConverter>
{
    /// <summary>Resolves a known key, falls back to translating the text, and leaves anything else untouched.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            string text when UiTextCatalog.HasKey(text) => UiTextCatalog.Get(text),
            string text => UiTextCatalog.TranslateRuntimeText(text),
            Enum option => UiTextCatalog.HasKey(option.ToString())
                ? UiTextCatalog.Get(option.ToString())
                : UiTextCatalog.TranslateRuntimeText(option.ToString()),
            _ => value
        };
    }

    /// <summary>Not supported: translated text is display only.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Localized display text cannot be written to source state.");
    }

    /// <summary>Returns the converter itself for XAML markup usage.</summary>
    public IValueConverter ProvideValue(IServiceProvider serviceProvider) => this;

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => this;
}
