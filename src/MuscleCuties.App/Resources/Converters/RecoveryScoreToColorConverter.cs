using System.Globalization;

namespace MuscleCuties.App.Resources.Converters;

public class RecoveryScoreToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var score = value is int i ? i : 0;
        return score >= 70 ? Color.FromArgb("#4CAF50")
             : score >= 40 ? Color.FromArgb("#FF9800")
             :               Color.FromArgb("#F44336");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
