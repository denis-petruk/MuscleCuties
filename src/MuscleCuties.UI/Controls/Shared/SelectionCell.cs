#if IOS
using UIKit;
#endif

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// The one selection cell used by every choice surface. It is a Border with a real selection
/// property so <c>ChoiceCell</c> can own the filled state, stroke and spoken state in one place
/// while each surface only projects its own flag.
/// </summary>
public sealed class SelectionCell : Border
{
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected),
        typeof(bool),
        typeof(SelectionCell),
        false,
        propertyChanged: static (bindable, _, _) =>
            ((SelectionCell)bindable).ApplyAccessibilityTraits());

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Refreshes the selection visuals once the cell is attached.</summary>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        ApplyAccessibilityTraits();
    }

    /// <summary>
    ///     A selection cell is a real button on iOS, with the selected trait carrying the current
    ///     choice independently of the visual checkmark. SemanticProperties supplies the localized
    ///     label and hint; the native trait supplies the button role and selected state VoiceOver
    ///     announces.
    /// </summary>
    private void ApplyAccessibilityTraits()
    {
#if IOS
        if (Handler?.PlatformView is not UIView nativeView)
            return;

        nativeView.IsAccessibilityElement = true;
        var traits = nativeView.AccessibilityTraits | UIAccessibilityTrait.Button;
        traits = IsSelected
            ? traits | UIAccessibilityTrait.Selected
            : traits & ~UIAccessibilityTrait.Selected;
        nativeView.AccessibilityTraits = traits;
#endif
    }
}
