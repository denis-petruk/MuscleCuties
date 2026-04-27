using System.Globalization;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.App.Resources.Converters;

public class CyclePhaseToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CyclePhase phase)
            return new SolidColorBrush(Colors.Transparent);

        return phase switch
        {
            CyclePhase.Menstrual  => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#FFD4E0"), 0f),
                    new GradientStop(Color.FromArgb("#FFC0CB"), 1f)
                },
                new Point(0, 0), new Point(1, 1)),
            CyclePhase.Follicular => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#D4F0D4"), 0f),
                    new GradientStop(Color.FromArgb("#A8E6CF"), 1f)
                },
                new Point(0, 0), new Point(1, 1)),
            CyclePhase.Ovulatory  => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#FFF3CD"), 0f),
                    new GradientStop(Color.FromArgb("#FFE08A"), 1f)
                },
                new Point(0, 0), new Point(1, 1)),
            CyclePhase.Luteal     => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#E8D4F0"), 0f),
                    new GradientStop(Color.FromArgb("#D4A8E8"), 1f)
                },
                new Point(0, 0), new Point(1, 1)),
            _ => new SolidColorBrush(Colors.Transparent)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
