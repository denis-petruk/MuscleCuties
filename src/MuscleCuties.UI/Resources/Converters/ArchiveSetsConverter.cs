using System.Globalization;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Renders a set count with Russian plural forms instead of `1 sets` (D26).</summary>
public sealed class ArchiveSetsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int count || count <= 0)
            return UiTextCatalog.Get("Workout_History_NotRecorded");
        var key = count % 100 is >= 11 and <= 14
            ? "Workout_History_SetsManyFormat"
            : count % 10 == 1
                ? "Workout_History_SetsOneFormat"
                : count % 10 is 2 or 3 or 4
                    ? "Workout_History_SetsFewFormat"
                    : "Workout_History_SetsManyFormat";
        return UiTextCatalog.Format(key, count);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
