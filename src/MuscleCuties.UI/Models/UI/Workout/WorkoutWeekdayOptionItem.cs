using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class WorkoutWeekdayOptionItem(DayOfWeek dayOfWeek) : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public DayOfWeek DayOfWeek { get; } = dayOfWeek;

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
