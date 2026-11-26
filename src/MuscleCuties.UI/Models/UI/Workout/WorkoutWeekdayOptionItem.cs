
namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One weekday in the training-day chooser, labelled in the app language.</summary>
public partial class WorkoutWeekdayOptionItem(DayOfWeek dayOfWeek) : ObservableObject
{
    // Four cells per row: the shared card inset (19pt per side) leaves too little width for a
    // localized day token in a seven-across strip, so the chooser wraps instead of stacking
    // characters. The label grows downward at large text sizes.
    [ObservableProperty] private bool _isSelected;

    /// <summary>The weekday the option stands for.</summary>
    public DayOfWeek DayOfWeek { get; } = dayOfWeek;

    /// <summary>The same calendar glyph the onboarding quiz uses, so both choosers read as one control.</summary>
    public string IconGlyph { get; } = "CalendarDay24";
    /// <summary>The full weekday label.</summary>
    public string Label { get; } = UiTextCatalog.Get(dayOfWeek switch
    {
        DayOfWeek.Monday => "Workout_Generation_FullDayMonday",
        DayOfWeek.Tuesday => "Workout_Generation_FullDayTuesday",
        DayOfWeek.Wednesday => "Workout_Generation_FullDayWednesday",
        DayOfWeek.Thursday => "Workout_Generation_FullDayThursday",
        DayOfWeek.Friday => "Workout_Generation_FullDayFriday",
        DayOfWeek.Saturday => "Workout_Generation_FullDaySaturday",
        _ => "Workout_Generation_FullDaySunday"
    });
}
