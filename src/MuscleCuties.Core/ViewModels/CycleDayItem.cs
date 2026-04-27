using Microsoft.Maui.Graphics;

namespace MuscleCuties.Core.ViewModels;

public class CycleDayItem
{
    public int Day { get; set; }
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
    public Color StrokeColor { get; set; } = Colors.Transparent;
    public double StrokeThickness { get; set; }
}
