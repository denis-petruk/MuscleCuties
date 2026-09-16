using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutSessionModal : AnimatedModalView
{
    private bool _hasContent;

    protected override void PrepareForOpen()
    {
        if (_hasContent)
            return;

        InitializeComponent();
        _hasContent = true;
    }
}
