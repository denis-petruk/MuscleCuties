using System.Windows.Input;

namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutExerciseCard : ContentView
{
    public static readonly BindableProperty OpenDetailCommandProperty =
        BindableProperty.Create(nameof(OpenDetailCommand), typeof(ICommand), typeof(WorkoutExerciseCard));

    public static readonly BindableProperty LogExerciseCommandProperty =
        BindableProperty.Create(nameof(LogExerciseCommand), typeof(ICommand), typeof(WorkoutExerciseCard));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(WorkoutExerciseCard));

    public WorkoutExerciseCard()
    {
        InitializeComponent();
    }

    public ICommand? OpenDetailCommand
    {
        get => (ICommand?)GetValue(OpenDetailCommandProperty);
        set => SetValue(OpenDetailCommandProperty, value);
    }

    public ICommand? LogExerciseCommand
    {
        get => (ICommand?)GetValue(LogExerciseCommandProperty);
        set => SetValue(LogExerciseCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
}
