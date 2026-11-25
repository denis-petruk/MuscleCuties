using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// One time-of-day value slot: a single InputContainer frame around a 24-hour picker. The value is
/// owned by the caller and never refreshed on a timer, so a sheet opened at 08:58 keeps showing
/// 08:58 while the reader chooses a meal instead of drifting to 08:59 beneath the buttons.
/// </summary>
public partial class TimeField : ContentView
{
    public static readonly BindableProperty TimeProperty = BindableProperty.Create(
        nameof(Time),
        typeof(TimeSpan),
        typeof(TimeField),
        TimeSpan.Zero,
        BindingMode.TwoWay);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description),
        typeof(string),
        typeof(TimeField),
        string.Empty);

    public static readonly BindableProperty HintProperty = BindableProperty.Create(
        nameof(Hint),
        typeof(string),
        typeof(TimeField),
        string.Empty);

    /// <summary>Initializes the time field declared in its XAML.</summary>
    public TimeField()
    {
        InitializeComponent();
    }

    public TimeSpan Time
    {
        get => (TimeSpan)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }
}
