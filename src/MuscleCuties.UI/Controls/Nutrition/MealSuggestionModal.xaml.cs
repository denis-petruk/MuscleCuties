
namespace MuscleCuties.UI.Controls.Nutrition;

/// <summary>
///     Sheet listing meal suggestions for the selected meal type and time.
/// </summary>
public partial class MealSuggestionModal : AnimatedModalView
{
    /// <summary>Initializes the modal declared in its XAML.</summary>
    public MealSuggestionModal()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;

    /// <summary>Switches the suggestions to two columns once the sheet is wide enough.</summary>
    private void OnSuggestionsSizeChanged(object? sender, EventArgs e)
    {
        // Keep each suggestion readable when names and nutrition details wrap.
        SuggestionLayout.Span = SuggestionCollection.Width >= AppThemeResources.GetDouble("UiDimension760") ? 2 : 1;
    }
}
