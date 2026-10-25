using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class MealSuggestionModal : AnimatedModalView
{
    public MealSuggestionModal()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }

    private void OnSuggestionsSizeChanged(object? sender, EventArgs e)
    {
        // Keep each suggestion readable when names and nutrition details wrap.
        SuggestionLayout.Span = SuggestionCollection.Width >= AppThemeResources.GetDouble("UiDimension760") ? 2 : 1;
    }
}
