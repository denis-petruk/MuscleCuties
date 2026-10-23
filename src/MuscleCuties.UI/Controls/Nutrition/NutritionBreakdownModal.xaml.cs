using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class NutritionBreakdownModal : AnimatedModalView
{
    public NutritionBreakdownModal()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }
}
