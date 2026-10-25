using System.Collections.ObjectModel;
using System.Windows.Input;
using MauiIcons.Core;
using MauiIcons.Fluent;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Workout;

public partial class FeaturedWorkoutCard : ContentView
{
    private const int SingleActivityCount = 1;
    private const int FirstActivityIndex = 0;
    private const int SecondActivityIndex = 1;
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

    public static readonly BindableProperty IsActionVisibleProperty =
        BindableProperty.Create(nameof(IsActionVisible), typeof(bool), typeof(FeaturedWorkoutCard), false);

    public static readonly BindableProperty IsEditActionProperty =
        BindableProperty.Create(nameof(IsEditAction), typeof(bool), typeof(FeaturedWorkoutCard), false);

    public static readonly BindableProperty IsRestDayProperty =
        BindableProperty.Create(nameof(IsRestDay), typeof(bool), typeof(FeaturedWorkoutCard), false);

    public static readonly BindableProperty AccentColorProperty =
        BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(FeaturedWorkoutCard),
            default);

    public static readonly BindableProperty AccentTextColorProperty =
        BindableProperty.Create(nameof(AccentTextColor), typeof(Color), typeof(FeaturedWorkoutCard),
            default);

    public static readonly BindableProperty StartCommandProperty =
        BindableProperty.Create(nameof(StartCommand), typeof(ICommand), typeof(FeaturedWorkoutCard));

    public static readonly BindableProperty ActivitySectionsProperty =
        BindableProperty.Create(nameof(ActivitySections), typeof(ObservableCollection<WorkoutActivitySection>),
            typeof(FeaturedWorkoutCard), null, propertyChanged: OnActivitySectionsChanged);

    public static readonly BindableProperty HasMultipleActivitiesProperty =
        BindableProperty.Create(nameof(HasMultipleActivities), typeof(bool), typeof(FeaturedWorkoutCard), false);

    public static readonly BindableProperty ExercisePreviewsProperty =
        BindableProperty.Create(nameof(ExercisePreviews), typeof(ObservableCollection<ExercisePreviewItem>),
            typeof(FeaturedWorkoutCard), null, propertyChanged: OnExercisePreviewsChanged);

    public static readonly BindableProperty IsPreviewExpandedProperty =
        BindableProperty.Create(nameof(IsPreviewExpanded), typeof(bool), typeof(FeaturedWorkoutCard), false,
            propertyChanged: OnIsPreviewExpandedChanged);

    public static readonly BindableProperty TogglePreviewCommandProperty =
        BindableProperty.Create(nameof(TogglePreviewCommand), typeof(ICommand), typeof(FeaturedWorkoutCard));

    public FeaturedWorkoutCard()
    {
        InitializeComponent();
        AccentColor = AppThemeResources.GetColor("Secondary", "CardSurfaceDark");
        AccentTextColor = AppThemeResources.GetColor("TextAccent", "SecondaryDarkText");
        TogglePreviewCommand = new Command(() => IsPreviewExpanded = !IsPreviewExpanded);
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

    public bool IsActionVisible
    {
        get => (bool)GetValue(IsActionVisibleProperty);
        set => SetValue(IsActionVisibleProperty, value);
    }

    public bool IsEditAction
    {
        get => (bool)GetValue(IsEditActionProperty);
        set => SetValue(IsEditActionProperty, value);
    }

    public bool IsRestDay
    {
        get => (bool)GetValue(IsRestDayProperty);
        set => SetValue(IsRestDayProperty, value);
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

    public bool HasActivities => ActivitySections is { Count: > 0 };
    public bool HasSingleActivity => ActivitySections?.Count == SingleActivityCount;

    public ObservableCollection<ExercisePreviewItem>? ExercisePreviews
    {
        get => (ObservableCollection<ExercisePreviewItem>?)GetValue(ExercisePreviewsProperty);
        set => SetValue(ExercisePreviewsProperty, value);
    }

    public bool IsPreviewExpanded
    {
        get => (bool)GetValue(IsPreviewExpandedProperty);
        set => SetValue(IsPreviewExpandedProperty, value);
    }

    public ICommand? TogglePreviewCommand
    {
        get => (ICommand?)GetValue(TogglePreviewCommandProperty);
        set => SetValue(TogglePreviewCommandProperty, value);
    }

    public bool HasExercisePreviews => ExercisePreviews is { Count: > 0 };

    public string ExercisePreviewsCountText => ExercisePreviews != null && ExercisePreviews.Count > 0
        ? $"({ExercisePreviews.Count} exercises)"
        : string.Empty;

    public string PreviewChevron => IsPreviewExpanded ? "▲ Hide" : "▼ Preview";

    public ImageSource ActionIcon => (IsEditAction ? FluentIcons.Edit24 : FluentIcons.Play24)
        .ToImageSource(AppThemeResources.GetColor("Black"), AppThemeResources.GetDouble("UiDimension15"));

    public string PrimaryActivityTitle { get; private set; } = string.Empty;
    public string PrimaryActivityCount { get; private set; } = string.Empty;
    public Color PrimaryActivityBackground { get; private set; } = AppThemeResources.GetRequired<Color>("Transparent");
    public Color PrimaryActivityTextColor { get; private set; } = AppThemeResources.GetRequired<Color>("TextPrimary");

    public string SecondaryActivityTitle { get; private set; } = string.Empty;
    public string SecondaryActivityCount { get; private set; } = string.Empty;
    public Color SecondaryActivityBackground { get; private set; } = AppThemeResources.GetRequired<Color>("Transparent");
    public Color SecondaryActivityTextColor { get; private set; } = AppThemeResources.GetRequired<Color>("TextPrimary");

    private static void OnExercisePreviewsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FeaturedWorkoutCard card)
        {
            card.OnPropertyChanged(nameof(HasExercisePreviews));
            card.OnPropertyChanged(nameof(ExercisePreviewsCountText));
        }
    }

    private static void OnIsPreviewExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FeaturedWorkoutCard card) card.OnPropertyChanged(nameof(PreviewChevron));
    }

    private static void OnActivitySectionsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FeaturedWorkoutCard card)
            card.RefreshActivityProperties();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(HasMultipleActivities))
            OnPropertyChanged(nameof(HasSingleActivity));
        else if (propertyName == nameof(IsEditAction)) OnPropertyChanged(nameof(ActionIcon));
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
            var primary = sections[FirstActivityIndex];
            PrimaryActivityTitle = ShortActivityTitle(primary);
            PrimaryActivityCount = primary.ExerciseCountText;
            PrimaryActivityBackground = primary.Background;
            PrimaryActivityTextColor = primary.TextColor;

            if (sections.Count > SingleActivityCount)
            {
                var secondary = sections[SecondActivityIndex];
                SecondaryActivityTitle = ShortActivityTitle(secondary);
                SecondaryActivityCount = secondary.ExerciseCountText;
                SecondaryActivityBackground = secondary.Background;
                SecondaryActivityTextColor = secondary.TextColor;
            }
        }

        OnPropertyChanged(nameof(PrimaryActivityTitle));
        OnPropertyChanged(nameof(HasActivities));
        OnPropertyChanged(nameof(HasSingleActivity));
        OnPropertyChanged(nameof(PrimaryActivityCount));
        OnPropertyChanged(nameof(PrimaryActivityBackground));
        OnPropertyChanged(nameof(PrimaryActivityTextColor));
        OnPropertyChanged(nameof(SecondaryActivityTitle));
        OnPropertyChanged(nameof(SecondaryActivityCount));
        OnPropertyChanged(nameof(SecondaryActivityBackground));
        OnPropertyChanged(nameof(SecondaryActivityTextColor));
    }

    private static string ShortActivityTitle(WorkoutActivitySection section) => section.Tag switch
    {
        "CARDIO" => "Cardio",
        "RECOVERY" => "Recovery",
        "REST" => "Rest",
        "STRENGTH" => "Strength",
        _ => section.Title
    };
}
