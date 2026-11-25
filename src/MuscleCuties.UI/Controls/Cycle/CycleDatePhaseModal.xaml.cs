
namespace MuscleCuties.UI.Controls.Cycle;

/// <summary>
///     Sheet that explains the phase assigned to a chosen calendar date.
/// </summary>
public partial class CycleDatePhaseModal : AnimatedModalView
{
    /// <summary>Initializes the modal declared in its XAML.</summary>
    public CycleDatePhaseModal()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to its content.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.ContentFit;
}
