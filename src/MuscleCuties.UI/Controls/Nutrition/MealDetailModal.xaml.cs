using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class MealDetailModal : AnimatedModalView
{
    public MealDetailModal()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }
}
