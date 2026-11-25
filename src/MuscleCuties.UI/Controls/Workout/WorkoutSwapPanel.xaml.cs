
namespace MuscleCuties.UI.Controls.Workout;

/// <summary>
///     Sheet for swapping a planned exercise for an alternative.
/// </summary>
public partial class WorkoutSwapPanel : AnimatedModalView
{
    /// <summary>Initializes the panel declared in its XAML.</summary>
    public WorkoutSwapPanel()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to its content.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.ContentFit;
}
