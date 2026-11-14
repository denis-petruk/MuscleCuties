using System.Globalization;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Renders archived seconds as localized minutes instead of the raw `1800s` (D26).</summary>
public sealed class ArchiveDurationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int seconds || seconds <= 0)
            return UiTextCatalog.Get("Workout_History_NotRecorded");
        var minutes = (int)Math.Round(seconds / 60d);
        return UiTextCatalog.Format("Workout_History_DurationValueFormat", minutes);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
