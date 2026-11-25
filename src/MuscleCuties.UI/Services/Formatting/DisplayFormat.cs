
namespace MuscleCuties.UI.Services.Formatting;

/// <summary>
///     The single place user-visible quantities, nutrient amounts, weights, durations, dates and
///     clocks are rendered (D3, D10, D26, D28, D33, D38). One culture-aware decimal separator, one
///     localized unit suffix and one date/clock shape, so two surfaces cannot describe the same
///     value differently.
/// </summary>
public static class DisplayFormat
{
    public const string ClockPattern = "HH:mm";
    public const string DayMonthPattern = "d MMM";
    public const string DayMonthLongPattern = "d MMMM";
    public const string DatePattern = "d MMM yyyy";
    public const string DateWithWeekdayPattern = "ddd, d MMM yyyy";
    public const string DayMonthWithWeekdayPattern = "ddd, d MMM";
    public const string MonthYearPattern = "MMM yyyy";
    public const string MonthYearFullPattern = "MMMM yyyy";

    /// <summary>Grams with their unit.</summary>
    public static string Grams(double grams) => UiTextCatalog.Format("Nutrition_Format_Grams", grams);

    /// <summary>Grams in their short form.</summary>
    public static string GramsShort(double grams) => UiTextCatalog.Format("Nutrition_Format_GramsShort", grams);

    /// <summary>Whole grams in their short form.</summary>
    public static string GramsWholeShort(double grams) => UiTextCatalog.Format("Nutrition_Format_GramsWholeShort", grams);

    /// <summary>Milligrams with their unit.</summary>
    public static string Milligrams(double milligrams) => UiTextCatalog.Format("Nutrition_Format_Milligrams", milligrams);

    /// <summary>Micrograms with their unit.</summary>
    public static string Micrograms(double micrograms) => UiTextCatalog.Format("Nutrition_Format_Micrograms", micrograms);

    /// <summary>A calorie value.</summary>
    public static string Calories(double kilocalories) => UiTextCatalog.Format("Nutrition_Format_Calories", kilocalories);

    /// <summary>A micronutrient amount with its unit.</summary>
    public static string Micronutrient(double amount, string unit) =>
        UiTextCatalog.Format("Nutrition_Format_QuantityWithUnit", amount, UnitLabel(unit));

    /// <summary>The protein, carbs and fats line.</summary>
    public static string Macros(double protein, double carbs, double fats) =>
        UiTextCatalog.Format("Nutrition_Format_Macros", protein, carbs, fats);

    /// <summary>An hour count.</summary>
    public static string Hours(double hours) => UiTextCatalog.Format("Common_Format_Hours", hours);

    /// <summary>A minute count.</summary>
    public static string Minutes(double minutes) => UiTextCatalog.Format("Common_Format_Minutes", minutes);

    /// <summary>A measurement with its unit label.</summary>
    public static string Measurement(double value, string unitLabel) =>
        UiTextCatalog.Format("Common_Format_QuantityWithUnit", value, unitLabel);

    /// <summary>A weight in kilograms.</summary>
    public static string WeightKg(double kilograms) =>
        UiTextCatalog.Format("Workout_History_ValueKgFormat", Number(kilograms));

    /// <summary>A weight in kilograms with one more decimal.</summary>
    public static string WeightKgPrecise(double kilograms) =>
        UiTextCatalog.Format("Workout_History_ValueKgFormat", Decimal1(kilograms));

    /// <summary>A distance in kilometres.</summary>
    public static string DistanceKm(double kilometres) =>
        UiTextCatalog.Format("Workout_History_DistanceFormat", kilometres);

    /// <summary>A volume, shown in tonnes once it passes a thousand kilos.</summary>
    public static string SummaryVolume(double kilograms) => kilograms >= 1000
        ? UiTextCatalog.Format("Common_Format_Tonnes", Decimal1(kilograms / 1000))
        : UiTextCatalog.Format("Common_Format_Kilograms", Decimal1(kilograms));

    /// <summary>A number with no decimals.</summary>
    public static string Number(double value) => value.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>A number with one decimal.</summary>
    public static string Decimal1(double value) => value.ToString("N1", CultureInfo.CurrentCulture);

    /// <summary>A number with up to two decimals.</summary>
    public static string Decimal2(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>A decimal with up to two decimals.</summary>
    public static string Decimal2(decimal value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>A number with up to four decimals.</summary>
    public static string Decimal4(double value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    /// <summary>A number rounded to a whole.</summary>
    public static string WholeNumber(double value) => Math.Round(value).ToString("0", CultureInfo.CurrentCulture);

    /// <summary>The clock time of a timestamp.</summary>
    public static string Time(DateTime value) => value.ToString(ClockPattern, CultureInfo.InvariantCulture);

    // TimeSpan has no HH token: its 24-hour clock token is hh, so the colon must be escaped.
    /// <summary>A duration as hours and minutes.</summary>
    public static string Time(TimeSpan value) => value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>A countdown as minutes and seconds.</summary>
    public static string Timer(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    /// <summary>The date of a timestamp.</summary>
    public static string Date(DateTime value) => value.ToString(DatePattern, CultureInfo.CurrentCulture);

    /// <summary>The date of a date value.</summary>
    public static string Date(DateOnly value) => value.ToDateTime(TimeOnly.MinValue).ToString(DatePattern, CultureInfo.CurrentCulture);

    /// <summary>The date with its weekday.</summary>
    public static string DateWithWeekday(DateTime value) =>
        value.ToString(DateWithWeekdayPattern, CultureInfo.CurrentCulture);

    /// <summary>The date with its weekday and stamp.</summary>
    public static string DateWithWeekdayStamp(DateTime value) =>
        $"{DateWithWeekday(value)} · {Time(value)}";

    /// <summary>The day and month of a timestamp.</summary>
    public static string DayMonth(DateTime value) => value.ToString(DayMonthPattern, CultureInfo.CurrentCulture);

    /// <summary>The day and month of a date value.</summary>
    public static string DayMonth(DateOnly value) =>
        value.ToDateTime(TimeOnly.MinValue).ToString(DayMonthPattern, CultureInfo.CurrentCulture);

    /// <summary>The day and month with the weekday.</summary>
    public static string DayMonthWithWeekday(DateTime value) =>
        value.ToString(DayMonthWithWeekdayPattern, CultureInfo.CurrentCulture);

    /// <summary>The day and month with the weekday of a date value.</summary>
    public static string DayMonthWithWeekday(DateOnly value) =>
        value.ToDateTime(TimeOnly.MinValue).ToString(DayMonthWithWeekdayPattern, CultureInfo.CurrentCulture);

    /// <summary>The long day and month with the weekday.</summary>
    public static string DayMonthLongWithWeekday(DateTime value) =>
        value.ToString($"dddd, {DayMonthLongPattern}", CultureInfo.CurrentCulture);

    /// <summary>The month and year.</summary>
    public static string MonthYear(DateTime value) => value.ToString(MonthYearPattern, CultureInfo.CurrentCulture);

    /// <summary>Month and year with the month capitalised, because several languages' month names</summary>
    /// are lower case and this value heads a section.</summary>
    public static string MonthYearFull(DateTime value)
    {
        var text = value.ToString(MonthYearFullPattern, CultureInfo.CurrentCulture);
        return text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
    }

    /// <summary>The full date, spelled out.</summary>
    public static string FullDate(DateTime value) => value.ToString("D", CultureInfo.CurrentCulture);

    /// <summary>The short date.</summary>
    public static string ShortDate(DateTime value) => value.ToString("d", CultureInfo.CurrentCulture);

    /// <summary>The short weekday name.</summary>
    public static string DayOfWeekShort(DateTime value) =>
        value.ToString("ddd", CultureInfo.CurrentCulture);

    /// <summary>The weekday name in capitals.</summary>
    public static string DayOfWeekShortUpper(DateTime value) =>
        DayOfWeekShort(value).ToUpperInvariant();

    /// <summary>The weekday's initial.</summary>
    public static string DayOfWeekInitial(DateTime value) =>
        DayOfWeekShort(value)[..1].ToUpperInvariant();

    /// <summary>The day of the month of a timestamp.</summary>
    public static string DayNumber(DateTime value) => value.Day.ToString(CultureInfo.InvariantCulture);

    /// <summary>The day of the month of a date value.</summary>
    public static string DayNumber(DateOnly value) => value.Day.ToString(CultureInfo.InvariantCulture);

    /// <summary>The day of the month, padded to two digits.</summary>
    public static string DayNumberPadded(DateTime value) =>
        value.Day.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>The short month name.</summary>
    public static string MonthShort(DateTime value) => value.ToString("MMM", CultureInfo.CurrentCulture);

    /// <summary>The localized label for a measurement unit.</summary>
    private static string UnitLabel(string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "mg" or "мг" => UiTextCatalog.Get("Nutrition_Unit_Milligrams"),
        "mcg" or "µg" or "ug" or "мкг" => UiTextCatalog.Get("Nutrition_Unit_Micrograms"),
        "g" or "г" => UiTextCatalog.Get("Nutrition_Unit_Grams"),
        _ => unit
    };
}
