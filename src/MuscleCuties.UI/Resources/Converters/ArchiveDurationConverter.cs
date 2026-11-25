
namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Renders archived seconds as localized minutes instead of the raw `1800s` (D26).</summary>
public sealed class ArchiveDurationConverter : IValueConverter
{
    /// <summary>Archived seconds as localized minutes; a missing or non-positive duration reads as "not recorded".</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int seconds || seconds <= 0)
            return UiTextCatalog.Get("Workout_History_NotRecorded");
        var minutes = (int)Math.Round(seconds / 60d);
        return UiTextCatalog.Format("Workout_History_DurationValueFormat", minutes);
    }

    /// <summary>Rejects the back conversion; the value only formats for display.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
