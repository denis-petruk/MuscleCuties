using Microsoft.Maui.Graphics;

namespace MuscleCuties.Core.ViewModels;

public class PhaseItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
}
