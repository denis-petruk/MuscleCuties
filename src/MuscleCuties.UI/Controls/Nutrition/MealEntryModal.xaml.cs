using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Nutrition;

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

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }
}
