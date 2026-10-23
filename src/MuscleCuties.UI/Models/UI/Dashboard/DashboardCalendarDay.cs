using Microsoft.Maui.Graphics;

namespace MuscleCuties.UI.Models.UI.Dashboard;

public sealed class DashboardCalendarDay
{
    public int Day { get; init; }
    public bool IsCurrentMonth { get; init; }
    public bool IsToday { get; init; }
    public Color PhaseColor { get; init; } = Colors.Transparent;
    public bool HasWorkout { get; init; }
    public bool HasPlannedWorkout { get; init; }
    public bool HasMealLogged { get; init; }
}
