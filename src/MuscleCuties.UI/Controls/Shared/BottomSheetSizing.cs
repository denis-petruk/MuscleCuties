
namespace MuscleCuties.UI.Controls.Shared;

/// <summary>How a sheet claims vertical space inside the modal viewport.</summary>
public enum SheetSizing
{
    /// <summary>The sheet keeps its own height policy and is left alone.</summary>
    None,

    /// <summary>The sheet fills the viewport up to the sheet ceiling.</summary>
    Fill,

    /// <summary>The sheet takes its content height, capped at the sheet ceiling.</summary>
    ContentFit
}

/// <summary>Sizes an app bottom sheet to the available modal viewport.</summary>
public static class BottomSheetSizing
{
    private const double AutomaticSize = -1;

    /// <summary>The tallest a sheet may grow, so a sheet never swallows the page behind it.</summary>
    public static double MaximumSheetHeight => AppThemeResources.GetDouble("UiDimension760");
    /// <summary>The least of the page that stays visible above a sheet.</summary>
    public static double MinimumTopClearance => AppThemeResources.GetDouble("UiDimension72");

    /// <summary>Sizes a sheet to its content, within the height and clearance limits.</summary>
    public static void Apply(VisualElement sheetSurface, double viewportHeight)
        => Apply(sheetSurface, viewportHeight, MaximumSheetHeight);

    /// <summary>Sizes a sheet to a height its content cannot measure itself, clamped to the same limits.</summary>
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
