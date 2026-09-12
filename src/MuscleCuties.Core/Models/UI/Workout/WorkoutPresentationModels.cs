using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Models.UI.Workout;

public class WorkoutItem
{
    public int WorkoutDayId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string DayLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string ExerciseCountText { get; set; } = string.Empty;
    public string ActivityCountText { get; set; } = string.Empty;
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

public sealed record TodaysWorkoutSummary(
    string Title,
    string Subtitle,
    string DurationText,
    string ExercisesCount,
    string Intensity,
    string SessionProgressText,
    string ActivityTag = "REST",
    IReadOnlyList<WorkoutActivitySection>? ActivitySections = null)
{
    public static TodaysWorkoutSummary RestDay { get; } =
        new(
            "Living happy life",
            "Pure rest day",
            "Rest day",
            "0",
            "None",
            "REST");
}

public sealed record WorkoutPlanSummary(
    WorkoutPlan? ActivePlan,
    IReadOnlyList<WorkoutDay> WorkoutDays,
    IReadOnlyList<WorkoutItem> Workouts);
