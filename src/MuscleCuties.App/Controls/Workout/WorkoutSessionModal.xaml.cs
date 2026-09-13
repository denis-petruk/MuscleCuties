using System.Diagnostics;
using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutSessionModal : ContentView
{
    private bool _hasContent;
    public static readonly BindableProperty IsOpenProperty =
        BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(WorkoutSessionModal), false,
            propertyChanged: OnIsOpenChanged);

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public WorkoutSessionModal()
    {
        IsVisible = false;
    }

    private static void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not WorkoutSessionModal modal)
            return;

        if ((bool)newValue)
        {
            if (!modal._hasContent)
            {
                var started = Stopwatch.GetTimestamp();
                modal.InitializeComponent();
                modal._hasContent = true;
                Trace.WriteLine($"[Performance][WorkoutSessionModal] Deferred view initialized in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
            }

            modal.IsVisible = true;
            ModalTransition.PlayShow(modal);
        }
        else
        {
            ModalTransition.PlayHide(modal, () => modal.IsVisible = false);
        }
    }
}
