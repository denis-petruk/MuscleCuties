using System.Globalization;
using MuscleCuties.App.Resources.Styles;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.App.Resources.Converters;

public class CyclePhaseToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CyclePhase phase)
            return new SolidColorBrush(Colors.Transparent);

        return new SolidColorBrush(phase switch
        {
            CyclePhase.Menstrual => AppThemeResources.GetColor(
                "CyclePhaseMenstrualLight",
                "CyclePhaseMenstrualDark"),
            CyclePhase.Follicular => AppThemeResources.GetColor(
                "CyclePhaseFollicularLight",
                "CyclePhaseFollicularDark"),
            CyclePhase.Ovulatory => AppThemeResources.GetColor(
                "CyclePhaseOvulatoryLight",
                "CyclePhaseOvulatoryDark"),
            CyclePhase.Luteal => AppThemeResources.GetColor(
                "CyclePhaseLutealLight",
                "CyclePhaseLutealDark"),
            _ => Colors.Transparent
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(CyclePhaseToBrushConverter)} does not support reverse conversion.");
    }
}
