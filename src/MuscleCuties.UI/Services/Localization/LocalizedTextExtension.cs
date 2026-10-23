using Microsoft.Maui.Controls.Xaml;

namespace MuscleCuties.UI.Services.Localization;

[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class LocalizedTextExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider)
    {
        return UiTextCatalog.Get(Key);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }
}
