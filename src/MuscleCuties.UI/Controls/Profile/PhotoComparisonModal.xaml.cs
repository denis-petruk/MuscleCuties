
namespace MuscleCuties.UI.Controls.Profile;

/// <summary>
///     Sheet that shows two progress photos side by side.
/// </summary>
public partial class PhotoComparisonModal : AnimatedModalView
{
    private const double MinimumComparisonFraction = 0.1d;
    private const double MaximumComparisonFraction = 0.9d;
    private const double InitialComparisonFraction = 0.5d;
    private const double WholeComparisonWidth = 1d;

    /// <summary>Initializes the modal and places the comparison divider.</summary>
    public PhotoComparisonModal()
    {
        InitializeComponent();
        UpdateComparisonDivider(InitialComparisonFraction);
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;

    /// <summary>Moves the divider to the slider's new position.</summary>
    private void OnComparisonSliderChanged(object? sender, ValueChangedEventArgs args)
    {
        UpdateComparisonDivider(args.NewValue);
    }

    /// <summary>Moves the before/after divider to a fraction of the image, so the two photos meet exactly at the handle.</summary>
    private void UpdateComparisonDivider(double fraction)
    {
        var clamped = Math.Clamp(fraction, MinimumComparisonFraction, MaximumComparisonFraction);
        ComparisonFirstColumn.Width = new GridLength(clamped, GridUnitType.Star);
        ComparisonSecondColumn.Width = new GridLength(WholeComparisonWidth - clamped, GridUnitType.Star);
    }
}
