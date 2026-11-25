
namespace MuscleCuties.UI.Controls.Nutrition;

/// <summary>
///     Sheet with the day's micronutrient coverage.
/// </summary>
public partial class NutritionBreakdownModal : AnimatedModalView
{
    /// <summary>Initializes the modal declared in its XAML.</summary>
    public NutritionBreakdownModal()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;
}
