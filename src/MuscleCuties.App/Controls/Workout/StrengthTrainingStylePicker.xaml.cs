using System.Windows.Input;

namespace MuscleCuties.App.Controls.Workout;

public partial class StrengthTrainingStylePicker : ContentView
{
    public static readonly BindableProperty SelectStyleCommandProperty = BindableProperty.Create(
        nameof(SelectStyleCommand),
        typeof(ICommand),
        typeof(StrengthTrainingStylePicker));

    public StrengthTrainingStylePicker()
    {
        InitializeComponent();
    }

    public ICommand? SelectStyleCommand
    {
        get => (ICommand?)GetValue(SelectStyleCommandProperty);
        set => SetValue(SelectStyleCommandProperty, value);
    }
}
