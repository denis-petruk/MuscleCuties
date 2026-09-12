using System.Globalization;
using MuscleCuties.App.Resources.Styles;

namespace MuscleCuties.App.Resources.Converters;

public class ScoreToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var score = value is int i ? i : 0;
        return score >= 70 ? AppThemeResources.GetColor("ReadinessHighLight", "ReadinessHighDark")
            : score >= 40 ? AppThemeResources.GetColor("ReadinessMediumLight", "ReadinessMediumDark")
            : AppThemeResources.GetColor("ReadinessLowLight", "ReadinessLowDark");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{GetType().Name} does not support reverse conversion.");
    }
}
