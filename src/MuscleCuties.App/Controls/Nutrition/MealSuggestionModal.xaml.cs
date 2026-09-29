using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Nutrition;

public partial class MealSuggestionModal : AnimatedModalView
{
    public MealSuggestionModal()
    {
        InitializeComponent();
    }

    private void OnSuggestionsSizeChanged(object? sender, EventArgs e)
    {
        // Keep each suggestion readable when names and nutrition details wrap.
        SuggestionLayout.Span = SuggestionCollection.Width >= 760 ? 2 : 1;
    }

}
