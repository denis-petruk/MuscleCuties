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
        SuggestionLayout.Span = SuggestionCollection.Width >= 500 ? 2 : 1;
    }

}
