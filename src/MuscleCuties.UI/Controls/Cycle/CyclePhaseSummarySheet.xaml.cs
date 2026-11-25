
namespace MuscleCuties.UI.Controls.Cycle;

/// <summary>
///     Sheet summarising one cycle phase and what to expect from it.
/// </summary>
public partial class CyclePhaseSummarySheet : AnimatedModalView
{
    /// <summary>Initializes the sheet declared in its XAML.</summary>
    public CyclePhaseSummarySheet()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;
}
