using System.Diagnostics;
using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutSessionModal : AnimatedModalView
{
    private bool _hasContent;

    protected override void PrepareForOpen()
    {
        if (_hasContent)
            return;

        var started = Stopwatch.GetTimestamp();
        InitializeComponent();
        _hasContent = true;
        Trace.WriteLine($"[Performance][WorkoutSessionModal] Deferred view initialized in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }
}
