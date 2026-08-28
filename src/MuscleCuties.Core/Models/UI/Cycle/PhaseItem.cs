using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.UI.Cycle;

public class PhaseItem
{
    public CyclePhase Phase { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IllustrationSource { get; set; } = string.Empty;
    public string IconSource { get; set; } = string.Empty;
    public bool UsesAnimatedIllustration { get; set; }
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
    public Color StrokeColor { get; set; } = Colors.Transparent;
    public double StrokeThickness { get; set; }
}
