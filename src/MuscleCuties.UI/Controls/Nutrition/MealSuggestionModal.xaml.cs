using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class MealSuggestionModal : AnimatedModalView
{
    private const double TwoColumnSuggestionMinimumWidth = 760;

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
        SuggestionLayout.Span = SuggestionCollection.Width >= TwoColumnSuggestionMinimumWidth ? 2 : 1;
    }
}
