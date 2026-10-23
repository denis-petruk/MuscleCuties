using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Workout;

public partial class WorkoutSwapPanel : ContentView
{
    public WorkoutSwapPanel()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }
}
