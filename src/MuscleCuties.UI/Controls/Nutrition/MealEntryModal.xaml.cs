using System.Diagnostics;
using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class MealEntryModal : AnimatedModalView
{
    private bool _hasContent;

    protected override void PrepareForOpen()
    {
        if (_hasContent) return;

        var started = Stopwatch.GetTimestamp();
        InitializeComponent();
        _hasContent = true;
        BottomSheetSizing.Apply(SheetSurface, Height);
        Trace.WriteLine(
            $"[Performance][MealEntryModal] Deferred view initialized in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (_hasContent)
            BottomSheetSizing.Apply(SheetSurface, height);
    }
}
