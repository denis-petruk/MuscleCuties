using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>Sizes an app bottom sheet to the available modal viewport.</summary>
public static class BottomSheetSizing
{
    public static double MaximumSheetHeight => AppThemeResources.GetDouble("UiDimension760");
    public static double MinimumTopClearance => AppThemeResources.GetDouble("UiDimension72");

    public static void Apply(VisualElement sheetSurface, double viewportHeight)
        => Apply(sheetSurface, viewportHeight, MaximumSheetHeight);

    public static void Apply(VisualElement sheetSurface, double viewportHeight, double preferredHeight)
    {
        var maximumSheetHeight = MaximumSheetHeight;
        var minimumTopClearance = MinimumTopClearance;
        if (!double.IsFinite(viewportHeight) || viewportHeight <= minimumTopClearance)
            return;

        var validPreferredHeight = double.IsFinite(preferredHeight) && preferredHeight > 0
            ? preferredHeight
            : maximumSheetHeight;
        var sheetHeight = Math.Min(Math.Min(maximumSheetHeight, validPreferredHeight),
            viewportHeight - minimumTopClearance);
        if (sheetSurface.HeightRequest != sheetHeight)
            sheetSurface.HeightRequest = sheetHeight;
    }
}
