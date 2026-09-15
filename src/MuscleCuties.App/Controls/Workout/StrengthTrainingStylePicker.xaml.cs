using System.Collections;
using System.Windows.Input;

namespace MuscleCuties.App.Controls.Workout;

public partial class StrengthTrainingStylePicker : ContentView
{
    public static readonly BindableProperty SelectStyleCommandProperty = BindableProperty.Create(
        nameof(SelectStyleCommand),
        typeof(ICommand),
        typeof(StrengthTrainingStylePicker));

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable),
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

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
}
