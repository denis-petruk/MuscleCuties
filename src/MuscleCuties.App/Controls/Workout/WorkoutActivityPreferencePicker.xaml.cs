using System.Collections;
using System.Windows.Input;

namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutActivityPreferencePicker : ContentView
{
    public static readonly BindableProperty ActivityGroupsProperty = BindableProperty.Create(
        nameof(ActivityGroups),
        typeof(IEnumerable),
        typeof(WorkoutActivityPreferencePicker));

    public static readonly BindableProperty ToggleActivityCommandProperty = BindableProperty.Create(
        nameof(ToggleActivityCommand),
        typeof(ICommand),
        typeof(WorkoutActivityPreferencePicker));

    public WorkoutActivityPreferencePicker()
    {
        InitializeComponent();
    }

    public IEnumerable? ActivityGroups
    {
        get => (IEnumerable?)GetValue(ActivityGroupsProperty);
        set => SetValue(ActivityGroupsProperty, value);
    }

    public ICommand? ToggleActivityCommand
    {
        get => (ICommand?)GetValue(ToggleActivityCommandProperty);
        set => SetValue(ToggleActivityCommandProperty, value);
    }
}
