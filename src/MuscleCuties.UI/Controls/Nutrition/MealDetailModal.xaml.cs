
namespace MuscleCuties.UI.Controls.Nutrition;

/// <summary>
///     Sheet with the ingredient breakdown of a logged meal.
/// </summary>
public partial class MealDetailModal : AnimatedModalView
{
    /// <summary>Initializes the modal declared in its XAML.</summary>
    public MealDetailModal()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;
}
