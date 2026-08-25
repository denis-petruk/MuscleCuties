using System.Windows.Input;

namespace MuscleCuties.App.Controls.Workout;

public partial class FeaturedWorkoutCard : ContentView
{
    public static readonly BindableProperty BadgeTextProperty =
        BindableProperty.Create(nameof(BadgeText), typeof(string), typeof(FeaturedWorkoutCard), string.Empty);

    public static readonly BindableProperty WorkoutTitleProperty =
        BindableProperty.Create(nameof(WorkoutTitle), typeof(string), typeof(FeaturedWorkoutCard), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(FeaturedWorkoutCard), string.Empty);

    public static readonly BindableProperty DurationTextProperty =
        BindableProperty.Create(nameof(DurationText), typeof(string), typeof(FeaturedWorkoutCard), string.Empty);

    public static readonly BindableProperty ExercisesCountProperty =
        BindableProperty.Create(nameof(ExercisesCount), typeof(string), typeof(FeaturedWorkoutCard), string.Empty);

    public static readonly BindableProperty IntensityProperty =
        BindableProperty.Create(nameof(Intensity), typeof(string), typeof(FeaturedWorkoutCard), string.Empty);

    public static readonly BindableProperty StartCommandProperty =
        BindableProperty.Create(nameof(StartCommand), typeof(ICommand), typeof(FeaturedWorkoutCard));

    public FeaturedWorkoutCard()
    {
        InitializeComponent();
    }

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public string WorkoutTitle
    {
        get => (string)GetValue(WorkoutTitleProperty);
        set => SetValue(WorkoutTitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string DurationText
    {
        get => (string)GetValue(DurationTextProperty);
        set => SetValue(DurationTextProperty, value);
    }

    public string ExercisesCount
    {
        get => (string)GetValue(ExercisesCountProperty);
        set => SetValue(ExercisesCountProperty, value);
    }

    public string Intensity
    {
        get => (string)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public ICommand? StartCommand
    {
        get => (ICommand?)GetValue(StartCommandProperty);
        set => SetValue(StartCommandProperty, value);
    }
}
