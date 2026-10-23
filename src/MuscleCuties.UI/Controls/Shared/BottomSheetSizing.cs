namespace MuscleCuties.UI.Controls.Shared;

/// <summary>Sizes an app bottom sheet to the available modal viewport.</summary>
public static class BottomSheetSizing
{
    public const double MaximumSheetHeight = 760;
    public const double MinimumTopClearance = 72;

    public static void Apply(VisualElement sheetSurface, double viewportHeight)
        => Apply(sheetSurface, viewportHeight, MaximumSheetHeight);

    public static void Apply(VisualElement sheetSurface, double viewportHeight, double preferredHeight)
    {
        if (!double.IsFinite(viewportHeight) || viewportHeight <= MinimumTopClearance)
            return;

        var validPreferredHeight = double.IsFinite(preferredHeight) && preferredHeight > 0
            ? preferredHeight
            : MaximumSheetHeight;
        var sheetHeight = Math.Min(Math.Min(MaximumSheetHeight, validPreferredHeight),
            viewportHeight - MinimumTopClearance);
        if (sheetSurface.HeightRequest != sheetHeight)
            sheetSurface.HeightRequest = sheetHeight;
    }
}
