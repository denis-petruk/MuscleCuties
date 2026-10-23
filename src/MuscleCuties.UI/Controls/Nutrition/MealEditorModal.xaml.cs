using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class MealEditorModal : AnimatedModalView
{
    public MealEditorModal()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }

    private void OnSearchSubmitted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
    }
}
