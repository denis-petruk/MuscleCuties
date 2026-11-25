
namespace MuscleCuties.UI.Resources.Converters;

/// <summary>
/// Shows the check-in prerequisite caption while submission is unavailable. At accessibility
/// text sizes the caption yields its space so the rating chips remain visible above the footer.
/// </summary>
public sealed class SubmitHintVisibilityConverter : IValueConverter
{
    /// <summary>True when the submit hint should be shown.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false && !AccessibilityDecoration.IsAccessibilityTextSize();

    /// <summary>Returns the value unchanged; the hint never drives the source.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
