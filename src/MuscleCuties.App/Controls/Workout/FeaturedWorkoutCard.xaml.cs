using System.Collections.ObjectModel;
using System.Windows.Input;
using MauiIcons.Core;
using MauiIcons.Fluent;
using MuscleCuties.App.Resources.Styles;
using MuscleCuties.Core.Models.UI.Workout;

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

    public static readonly BindableProperty ActionTextProperty =
        BindableProperty.Create(nameof(ActionText), typeof(string), typeof(FeaturedWorkoutCard), "Start workout");

    public static readonly BindableProperty AccentColorProperty =
        BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(FeaturedWorkoutCard),
            Colors.Transparent);

    public static readonly BindableProperty AccentTextColorProperty =
        BindableProperty.Create(nameof(AccentTextColor), typeof(Color), typeof(FeaturedWorkoutCard),
            Colors.Transparent);

    public static readonly BindableProperty StartCommandProperty =
        BindableProperty.Create(nameof(StartCommand), typeof(ICommand), typeof(FeaturedWorkoutCard));

    public static readonly BindableProperty ActivitySectionsProperty =
        BindableProperty.Create(nameof(ActivitySections), typeof(ObservableCollection<WorkoutActivitySection>),
            typeof(FeaturedWorkoutCard), null, propertyChanged: OnActivitySectionsChanged);

    public static readonly BindableProperty HasMultipleActivitiesProperty =
        BindableProperty.Create(nameof(HasMultipleActivities), typeof(bool), typeof(FeaturedWorkoutCard), false);

    public FeaturedWorkoutCard()
    {
        InitializeComponent();
        AccentColor = AppThemeResources.GetColor("Secondary", "CardSurfaceDark");
        AccentTextColor = AppThemeResources.GetColor("TextAccent", "SecondaryDarkText");
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

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public Color AccentTextColor
    {
        get => (Color)GetValue(AccentTextColorProperty);
        set => SetValue(AccentTextColorProperty, value);
    }

    public ICommand? StartCommand
    {
        get => (ICommand?)GetValue(StartCommandProperty);
        set => SetValue(StartCommandProperty, value);
    }

    public ObservableCollection<WorkoutActivitySection>? ActivitySections
    {
        get => (ObservableCollection<WorkoutActivitySection>?)GetValue(ActivitySectionsProperty);
        set => SetValue(ActivitySectionsProperty, value);
    }

    public bool HasMultipleActivities
    {
        get => (bool)GetValue(HasMultipleActivitiesProperty);
        set => SetValue(HasMultipleActivitiesProperty, value);
    }

    public bool HasSingleActivity => !HasMultipleActivities;

    public ImageSource ActionIcon => (ActionText == "Edit workout" ? FluentIcons.Edit24 : FluentIcons.Play24)
        .ToImageSource(Colors.White, 15d);

    public string PrimaryActivityTitle { get; private set; } = string.Empty;
    public string PrimaryActivityCount { get; private set; } = string.Empty;
    public Color PrimaryActivityBackground { get; private set; } = Colors.Transparent;
    public Color PrimaryActivityTextColor { get; private set; } = Colors.Black;

    public string SecondaryActivityTitle { get; private set; } = string.Empty;
    public string SecondaryActivityCount { get; private set; } = string.Empty;
    public Color SecondaryActivityBackground { get; private set; } = Colors.Transparent;
    public Color SecondaryActivityTextColor { get; private set; } = Colors.Black;

    private static void OnActivitySectionsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FeaturedWorkoutCard card)
            card.RefreshActivityProperties();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(HasMultipleActivities))
        {
            OnPropertyChanged(nameof(HasSingleActivity));
        }
        else if (propertyName == nameof(ActionText))
        {
            OnPropertyChanged(nameof(ActionIcon));
        }
    }

    private void RefreshActivityProperties()
    {
        var sections = ActivitySections;
        if (sections is null || sections.Count == 0)
        {
            PrimaryActivityTitle = string.Empty;
            PrimaryActivityCount = string.Empty;
            PrimaryActivityBackground = AccentColor;
            PrimaryActivityTextColor = AccentTextColor;
        }
        else
        {
            var primary = sections[0];
            PrimaryActivityTitle = primary.Title.Replace(" activity", "");
            PrimaryActivityCount = primary.ExerciseCountText;
            PrimaryActivityBackground = primary.Background;
            PrimaryActivityTextColor = primary.TextColor;

            if (sections.Count > 1)
            {
                var secondary = sections[1];
                SecondaryActivityTitle = secondary.Title.Replace(" activity", "");
                SecondaryActivityCount = secondary.ExerciseCountText;
                SecondaryActivityBackground = secondary.Background;
                SecondaryActivityTextColor = secondary.TextColor;
            }
        }

        OnPropertyChanged(nameof(PrimaryActivityTitle));
        OnPropertyChanged(nameof(PrimaryActivityCount));
        OnPropertyChanged(nameof(PrimaryActivityBackground));
        OnPropertyChanged(nameof(PrimaryActivityTextColor));
        OnPropertyChanged(nameof(SecondaryActivityTitle));
        OnPropertyChanged(nameof(SecondaryActivityCount));
        OnPropertyChanged(nameof(SecondaryActivityBackground));
        OnPropertyChanged(nameof(SecondaryActivityTextColor));
    }
}
