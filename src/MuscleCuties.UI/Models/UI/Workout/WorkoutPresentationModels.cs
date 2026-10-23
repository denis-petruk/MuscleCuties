using Microsoft.Maui.Graphics;

namespace MuscleCuties.UI.Models.UI.Workout;

public sealed class WorkoutItem
{
    public int WorkoutDayId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string DayLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int ExerciseCount { get; set; }
    public int ActivityCount { get; set; }
    public string DetailsText { get; set; } = string.Empty;
    public Color ActivityBackground { get; set; } = Colors.Transparent;
    public Color ActivityTextColor { get; set; } = Colors.Black;
    public bool IsRestDay { get; set; }
    public string SessionProgressText { get; set; } = "Upcoming";
    public bool IsCompleted { get; set; }
}

public sealed record WorkoutActivitySection(
    string Tag,
    string Title,
    int ExerciseCount,
    Color Background,
    Color TextColor)
{
    public string ExerciseCountText => ExerciseCount == 1 ? "1 exercise" : $"{ExerciseCount} exercises";
}
