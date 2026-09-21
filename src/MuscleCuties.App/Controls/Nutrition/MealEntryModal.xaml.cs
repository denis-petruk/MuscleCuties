using System.Diagnostics;
using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Nutrition;

public partial class MealEntryModal : AnimatedModalView
{
    private bool _hasContent;

    protected override void PrepareForOpen()
    {
        if (_hasContent) return;

        var started = Stopwatch.GetTimestamp();
        InitializeComponent();
        _hasContent = true;
        Trace.WriteLine($"[Performance][MealEntryModal] Deferred view initialized in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }
}
