using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Services.Workout.Planning;

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

    public static WorkoutItem FromPlanItem(WorkoutListItem item) =>
        new()
        {
            WorkoutDayId = item.WorkoutDayId,
            Tag = item.Tag,
            DayLabel = item.DayLabel,
            Title = item.Title,
            Duration = item.Duration,
            ExerciseCountText = item.ExerciseCountText,
            ActivityCountText = item.ActivityCountText,
            DetailsText = item.DetailsText,
            ActivityBackground = item.ActivityBackground,
            ActivityTextColor = item.ActivityTextColor,
            IsRestDay = item.IsRestDay,
            SessionProgressText = item.SessionProgressText,
            IsCompleted = item.IsCompleted
        };
}
