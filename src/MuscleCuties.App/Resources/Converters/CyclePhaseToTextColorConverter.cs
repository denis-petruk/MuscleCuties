using System.Globalization;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.App.Resources.Converters;

public class CyclePhaseToTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        if (value is not CyclePhase phase)
            return Color.FromArgb(isDark ? "#F8EEF4" : "#2B1D24");

        return phase switch
        {
            CyclePhase.Menstrual => Color.FromArgb(isDark ? "#F9D6D8" : "#7A3A48"),
            CyclePhase.Follicular => Color.FromArgb(isDark ? "#D6EED6" : "#3A6B3A"),
            CyclePhase.Ovulatory => Color.FromArgb(isDark ? "#FFF0C4" : "#7A6000"),
            CyclePhase.Luteal => Color.FromArgb(isDark ? "#E8D8F5" : "#5A3B80"),
            _ => Color.FromArgb(isDark ? "#F8EEF4" : "#2B1D24")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(CyclePhaseToTextColorConverter)} does not support reverse conversion.");
}
