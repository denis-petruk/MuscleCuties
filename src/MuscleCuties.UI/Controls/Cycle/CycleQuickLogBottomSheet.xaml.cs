
namespace MuscleCuties.UI.Controls.Cycle;

/// <summary>
///     Sheet for logging today's cycle observations.
/// </summary>
public partial class CycleQuickLogBottomSheet : AnimatedModalView
{
    /// <summary>Initializes the sheet declared in its XAML.</summary>
    public CycleQuickLogBottomSheet()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;
}
