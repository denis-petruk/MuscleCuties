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
        false);

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
}
