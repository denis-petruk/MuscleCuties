using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class WorkoutWeekdayOptionItem(DayOfWeek dayOfWeek) : ObservableObject
{
    private static readonly DayOfWeek[] CalendarOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    [ObservableProperty] private bool _isSelected;

    public DayOfWeek DayOfWeek { get; } = dayOfWeek;
    public int CalendarColumn => Array.IndexOf(CalendarOrder, DayOfWeek);

    public string ShortLabel { get; } = UiTextCatalog.Get(dayOfWeek switch
    {
        DayOfWeek.Monday => "Workout_Generation_DayMonday",
        DayOfWeek.Tuesday => "Workout_Generation_DayTuesday",
        DayOfWeek.Wednesday => "Workout_Generation_DayWednesday",
        DayOfWeek.Thursday => "Workout_Generation_DayThursday",
        DayOfWeek.Friday => "Workout_Generation_DayFriday",
        DayOfWeek.Saturday => "Workout_Generation_DaySaturday",
        _ => "Workout_Generation_DaySunday"
    });

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
