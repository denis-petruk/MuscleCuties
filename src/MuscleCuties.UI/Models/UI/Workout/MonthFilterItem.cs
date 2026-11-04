namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>
///     Model for month selection chips in WorkoutHistoryPage.
/// </summary>
public sealed record MonthFilterItem(
    int Year,
    int Month,
    string DisplayName,
    bool IsAllMonths = false)
{
    public bool IsSelected { get; set; }
}
