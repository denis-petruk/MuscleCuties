
namespace MuscleCuties.UI.Controls.Nutrition;

/// <summary>The add-or-edit entry sheet, built on the shared modal base so it inherits the focus and keyboard</summary>
/// behaviour.</summary>
public partial class MealEntryModal : AnimatedModalView
{
    /// <summary>
    /// NM01: the sheet initializes with its host. It used to initialize from an open callback, so the
    /// XAML that supplies its open binding did not exist when the host was shown and Add meal became an
    /// uninitialized full-page layer.
    /// </summary>
    public MealEntryModal()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;
}
