using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Cycle;

public partial class CycleQuickLogBottomSheet : AnimatedModalView
{
    public CycleQuickLogBottomSheet()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }
}
