
namespace MuscleCuties.UI.Controls.Profile;

public partial class PhotoComparisonModal : AnimatedModalView
{
    private const double MinimumComparisonFraction = 0.1d;
    private const double MaximumComparisonFraction = 0.9d;
    private const double InitialComparisonFraction = 0.5d;
    private const double WholeComparisonWidth = 1d;

    public PhotoComparisonModal()
    {
        InitializeComponent();
        UpdateComparisonDivider(InitialComparisonFraction);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }

    private void OnComparisonSliderChanged(object? sender, ValueChangedEventArgs args)
    {
        UpdateComparisonDivider(args.NewValue);
    }

    private void UpdateComparisonDivider(double fraction)
    {
        var clamped = Math.Clamp(fraction, MinimumComparisonFraction, MaximumComparisonFraction);
        ComparisonFirstColumn.Width = new GridLength(clamped, GridUnitType.Star);
        ComparisonSecondColumn.Width = new GridLength(WholeComparisonWidth - clamped, GridUnitType.Star);
    }
}
