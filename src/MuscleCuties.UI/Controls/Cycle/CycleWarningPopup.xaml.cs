
namespace MuscleCuties.UI.Controls.Cycle;

/// <summary>
///     Compact popup that reports a cycle safety or input warning.
/// </summary>
public partial class CycleWarningPopup : AnimatedModalView
{
    /// <summary>Initializes the popup declared in its XAML.</summary>
    public CycleWarningPopup()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the popup to its content.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.ContentFit;
}
