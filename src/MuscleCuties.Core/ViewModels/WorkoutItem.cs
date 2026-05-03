using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.ViewModels;

public class WorkoutItem
{
    public int WorkoutDayId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int ExerciseCount { get; set; }
    public WorkoutType WorkoutType { get; set; }
    public Color PhaseBackground { get; set; } = Colors.Transparent;
}
