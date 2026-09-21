using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Nutrition;

public partial class MealEditorModal : AnimatedModalView
{
    public MealEditorModal()
    {
        InitializeComponent();
    }

    private void OnSearchSubmitted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
    }
}
