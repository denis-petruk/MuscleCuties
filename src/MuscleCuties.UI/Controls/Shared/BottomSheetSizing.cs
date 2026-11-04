using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>Sizes an app bottom sheet to the available modal viewport.</summary>
public static class BottomSheetSizing
{
    private const double AutomaticSize = -1;

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

    /// <summary>
    /// Lets the sheet take its content height, capped at the same viewport limit used for filled sheets.
    /// Short messages stay short instead of stretching into an empty full-height panel.
    /// </summary>
    public static void ApplyContentFit(VisualElement sheetSurface, double viewportHeight)
    {
        var minimumTopClearance = MinimumTopClearance;
        if (!double.IsFinite(viewportHeight) || viewportHeight <= minimumTopClearance)
            return;

        var maximumHeight = Math.Min(MaximumSheetHeight, viewportHeight - minimumTopClearance);
        if (sheetSurface.HeightRequest != AutomaticSize)
            sheetSurface.HeightRequest = AutomaticSize;
        if (sheetSurface.MaximumHeightRequest != maximumHeight)
            sheetSurface.MaximumHeightRequest = maximumHeight;
    }
}
