using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UI.Models.UI.Cycle;

public class PhaseExplorerPillItem
{
    public CyclePhase Phase { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Window { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string IconSource { get; set; } = string.Empty;
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
    public Color AccentColor { get; set; } = Colors.Transparent;
    public bool IsActivePhase { get; set; }
}
