using System.Globalization;
using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>
/// Shows the check-in prerequisite caption while submission is unavailable. At accessibility
/// text sizes the caption yields its space so the rating chips remain visible above the footer.
/// </summary>
public sealed class SubmitHintVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false && !AccessibilityDecoration.IsAccessibilityTextSize();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
