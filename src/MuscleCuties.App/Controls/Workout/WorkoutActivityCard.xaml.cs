using System.Windows.Input;

namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutActivityCard : ContentView
{
    public static readonly BindableProperty OpenCommandProperty =
        BindableProperty.Create(nameof(OpenCommand), typeof(ICommand), typeof(WorkoutActivityCard));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(WorkoutActivityCard));

    public WorkoutActivityCard()
    {
        InitializeComponent();
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
}
