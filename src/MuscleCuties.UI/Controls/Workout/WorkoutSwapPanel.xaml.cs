using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Workout;

public partial class WorkoutSwapPanel : AnimatedModalView
{
    public WorkoutSwapPanel()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        // Content-fit, not the fixed sheet height: a swap panel with no candidates was stretching
        // into an empty full-height surface.
        BottomSheetSizing.ApplyContentFit(SheetSurface, height);
    }
}
