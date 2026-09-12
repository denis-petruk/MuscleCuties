using System.Globalization;
using MuscleCuties.App.Resources.Styles;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.App.Resources.Converters;

public class CyclePhaseToTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CyclePhase phase)
            return AppThemeResources.GetColor("TextPrimary", "TextPrimaryDark");

        return phase switch
        {
            CyclePhase.Menstrual => AppThemeResources.GetColor(
                "CyclePhaseMenstrualTextLight",
                "CyclePhaseMenstrualTextDark"),
            CyclePhase.Follicular => AppThemeResources.GetColor(
                "CyclePhaseFollicularTextLight",
                "CyclePhaseFollicularTextDark"),
            CyclePhase.Ovulatory => AppThemeResources.GetColor(
                "CyclePhaseOvulatoryTextLight",
                "CyclePhaseOvulatoryTextDark"),
            CyclePhase.Luteal => AppThemeResources.GetColor(
                "CyclePhaseLutealTextLight",
                "CyclePhaseLutealTextDark"),
            _ => AppThemeResources.GetColor("TextPrimary", "TextPrimaryDark")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException(
            $"{nameof(CyclePhaseToTextColorConverter)} does not support reverse conversion.");
    }
}
