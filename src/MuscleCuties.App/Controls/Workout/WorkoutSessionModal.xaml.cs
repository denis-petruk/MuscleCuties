using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutSessionModal : ContentView
{
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
        InitializeComponent();
        IsVisible = false;
    }

    private static void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not WorkoutSessionModal modal)
            return;

        if ((bool)newValue)
        {
            modal.IsVisible = true;
            ModalTransition.PlayShow(modal);
        }
        else
        {
            ModalTransition.PlayHide(modal, () => modal.IsVisible = false);
        }
    }
}
