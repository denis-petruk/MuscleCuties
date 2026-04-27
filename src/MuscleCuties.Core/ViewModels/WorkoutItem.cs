using Microsoft.Maui.Graphics;

namespace MuscleCuties.Core.ViewModels;

public class WorkoutItem
{
    public string Tag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public Color PhaseBackground { get; set; } = Colors.Transparent;
}
