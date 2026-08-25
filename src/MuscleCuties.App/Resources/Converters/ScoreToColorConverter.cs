using System.Globalization;

namespace MuscleCuties.App.Resources.Converters;

public abstract class ScoreToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var score = value is int i ? i : 0;
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        return score >= 70 ? Color.FromArgb(isDark ? "#7FD197" : "#58A873")
             : score >= 40 ? Color.FromArgb(isDark ? "#F0C15D" : "#D9A441")
             : Color.FromArgb(isDark ? "#F08B8B" : "#D16B6B");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{GetType().Name} does not support reverse conversion.");
}
