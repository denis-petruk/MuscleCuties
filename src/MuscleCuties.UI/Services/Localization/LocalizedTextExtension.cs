using Microsoft.Maui.Controls.Xaml;

namespace MuscleCuties.UI.Services.Localization;

[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
/// <summary>XAML markup extension that resolves a catalog key at parse time: <c>{loc:LocalizedText Key}</c></summary>
/// instead of a converter on a binding.</summary>
public sealed class LocalizedTextExtension : IMarkupExtension<string>
{
    /// <summary>Catalog key to resolve; also the extension's content property, so the key can be positional.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The localized text for the key, or the key itself when it is not registered.</summary>
    public string ProvideValue(IServiceProvider serviceProvider)
    {
        return UiTextCatalog.Get(Key);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }
}
