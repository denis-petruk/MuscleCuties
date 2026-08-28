using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.UI.Cycle;

public class CycleDayItem
{
    public int Day { get; set; }
    public int CycleDay { get; set; }
    public DateTime? Date { get; set; }
    public CyclePhase Phase { get; set; }
    public string IconSource { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsNeutral { get; set; }
    public bool HasPhaseShiftLog { get; set; }
    public bool IsPredictedFuture { get; set; }
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
    public Color StrokeColor { get; set; } = Colors.Transparent;
    public double StrokeThickness { get; set; }
}
