using System.Globalization;

namespace MuscleCuties.UI.Services.Formatting;

/// <summary>
/// The one clock rendering for a meal time.
/// </summary>
/// <remarks>
/// A meal time is a reading of the clock, not a locale greeting: the sheet's field, its summary
/// chip, the meal card and the editor header all describe the same moment. The culture-short
/// pattern (<c>"t"</c>) prints <c>8:58 PM</c> under an English device locale, which put two clocks
/// for one meal inside a single Russian sheet. Every surface therefore renders
/// <see cref="Pattern"/> in the invariant culture, so the value cannot drift with the device
/// locale or the surface it is shown on.
/// </remarks>
public static class MealTime
{
    /// <summary>24-hour clock used by every nutrition surface.</summary>
    public const string Pattern = "HH:mm";

    /// <summary>Renders a time of day as <c>HH:mm</c>, e.g. <c>19:15</c>.</summary>
    public static string Text(TimeSpan timeOfDay) =>
        timeOfDay.ToString(Pattern, CultureInfo.InvariantCulture);

    /// <summary>Renders a timestamp's time of day as <c>HH:mm</c>.</summary>
    public static string Text(DateTime timestamp) =>
        Text(timestamp.TimeOfDay);
}
