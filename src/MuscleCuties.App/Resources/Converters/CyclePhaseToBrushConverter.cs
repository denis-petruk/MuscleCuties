using System.Globalization;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.App.Resources.Converters;

public class CyclePhaseToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CyclePhase phase)
            return new SolidColorBrush(Colors.Transparent);

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        return phase switch
        {
            CyclePhase.Menstrual => new SolidColorBrush(Color.FromArgb(isDark ? "#5A3840" : "#F9D6D8")),
            CyclePhase.Follicular => new SolidColorBrush(Color.FromArgb(isDark ? "#2E5230" : "#D6EED6")),
            CyclePhase.Ovulatory => new SolidColorBrush(Color.FromArgb(isDark ? "#5A4A00" : "#FFF0C4")),
            CyclePhase.Luteal => new SolidColorBrush(Color.FromArgb(isDark ? "#3E2A58" : "#E8D8F5")),
            _ => new SolidColorBrush(Colors.Transparent)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(CyclePhaseToBrushConverter)} does not support reverse conversion.");
}
