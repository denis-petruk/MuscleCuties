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
    /// <summary>Highlights the month chip currently filtering the history list.</summary>
    public bool IsSelected { get; set; }
}
