using System.Globalization;

namespace MuscleCuties.UI.Services.Formatting;

/// <summary>
///     One shape for user-visible session dates (D33): the archive list row and the detail sheet header
///     share the day part, and the sheet adds the 24-hour time.
/// </summary>
public static class DateText
{
    public const string SessionDateFormat = "ddd, d MMM yyyy";

    public static string SessionDate(DateTime value) =>
        value.ToString(SessionDateFormat, CultureInfo.CurrentCulture);

    public static string SessionStamp(DateTime value) =>
        $"{SessionDate(value)} · {value.ToString("HH:mm", CultureInfo.CurrentCulture)}";
}
