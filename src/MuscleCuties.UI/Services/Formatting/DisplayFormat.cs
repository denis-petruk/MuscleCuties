using System.Globalization;
using MuscleCuties.UI.Services.Localization;

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

    public static string Grams(double grams) => UiTextCatalog.Format("Nutrition_Format_Grams", grams);

    public static string GramsShort(double grams) => UiTextCatalog.Format("Nutrition_Format_GramsShort", grams);

    public static string GramsWholeShort(double grams) => UiTextCatalog.Format("Nutrition_Format_GramsWholeShort", grams);

    public static string Milligrams(double milligrams) => UiTextCatalog.Format("Nutrition_Format_Milligrams", milligrams);

    public static string Micrograms(double micrograms) => UiTextCatalog.Format("Nutrition_Format_Micrograms", micrograms);

    public static string Calories(double kilocalories) => UiTextCatalog.Format("Nutrition_Format_Calories", kilocalories);

    public static string Micronutrient(double amount, string unit) =>
        UiTextCatalog.Format("Nutrition_Format_QuantityWithUnit", amount, UnitLabel(unit));

    public static string Macros(double protein, double carbs, double fats) =>
        UiTextCatalog.Format("Nutrition_Format_Macros", protein, carbs, fats);

    public static string Hours(double hours) => UiTextCatalog.Format("Common_Format_Hours", hours);

    public static string Minutes(double minutes) => UiTextCatalog.Format("Common_Format_Minutes", minutes);

    public static string Measurement(double value, string unitLabel) =>
        UiTextCatalog.Format("Common_Format_QuantityWithUnit", value, unitLabel);

    public static string WeightKg(double kilograms) =>
        UiTextCatalog.Format("Workout_History_ValueKgFormat", Number(kilograms));

    public static string WeightKgPrecise(double kilograms) =>
        UiTextCatalog.Format("Workout_History_ValueKgFormat", Decimal1(kilograms));

    public static string DistanceKm(double kilometres) =>
        UiTextCatalog.Format("Workout_History_DistanceFormat", kilometres);

    public static string SummaryVolume(double kilograms) => kilograms >= 1000
        ? UiTextCatalog.Format("Common_Format_Tonnes", Decimal1(kilograms / 1000))
        : UiTextCatalog.Format("Common_Format_Kilograms", Decimal1(kilograms));

    public static string Number(double value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Decimal1(double value) => value.ToString("N1", CultureInfo.CurrentCulture);

    public static string Decimal2(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    public static string Decimal2(decimal value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    public static string Decimal4(double value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    public static string WholeNumber(double value) => Math.Round(value).ToString("0", CultureInfo.CurrentCulture);

    public static string Time(DateTime value) => value.ToString(ClockPattern, CultureInfo.InvariantCulture);

    // TimeSpan has no HH token: its 24-hour clock token is hh, so the colon must be escaped.
    public static string Time(TimeSpan value) => value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    public static string Timer(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    public static string Date(DateTime value) => value.ToString(DatePattern, CultureInfo.CurrentCulture);

    public static string Date(DateOnly value) => value.ToDateTime(TimeOnly.MinValue).ToString(DatePattern, CultureInfo.CurrentCulture);

    public static string DateWithWeekday(DateTime value) =>
        value.ToString(DateWithWeekdayPattern, CultureInfo.CurrentCulture);

    public static string DateWithWeekdayStamp(DateTime value) =>
        $"{DateWithWeekday(value)} · {Time(value)}";

    public static string DayMonth(DateTime value) => value.ToString(DayMonthPattern, CultureInfo.CurrentCulture);

    public static string DayMonth(DateOnly value) =>
        value.ToDateTime(TimeOnly.MinValue).ToString(DayMonthPattern, CultureInfo.CurrentCulture);

    public static string DayMonthWithWeekday(DateTime value) =>
        value.ToString(DayMonthWithWeekdayPattern, CultureInfo.CurrentCulture);

    public static string DayMonthWithWeekday(DateOnly value) =>
        value.ToDateTime(TimeOnly.MinValue).ToString(DayMonthWithWeekdayPattern, CultureInfo.CurrentCulture);

    public static string DayMonthLongWithWeekday(DateTime value) =>
        value.ToString($"dddd, {DayMonthLongPattern}", CultureInfo.CurrentCulture);

    public static string MonthYear(DateTime value) => value.ToString(MonthYearPattern, CultureInfo.CurrentCulture);

    public static string MonthYearFull(DateTime value) =>
        value.ToString(MonthYearFullPattern, CultureInfo.CurrentCulture);

    public static string FullDate(DateTime value) => value.ToString("D", CultureInfo.CurrentCulture);

    public static string ShortDate(DateTime value) => value.ToString("d", CultureInfo.CurrentCulture);

    public static string DayOfWeekShort(DateTime value) =>
        value.ToString("ddd", CultureInfo.CurrentCulture);

    public static string DayOfWeekShortUpper(DateTime value) =>
        DayOfWeekShort(value).ToUpperInvariant();

    public static string DayOfWeekInitial(DateTime value) =>
        DayOfWeekShort(value)[..1].ToUpperInvariant();

    public static string DayNumber(DateTime value) => value.Day.ToString(CultureInfo.InvariantCulture);

    public static string DayNumber(DateOnly value) => value.Day.ToString(CultureInfo.InvariantCulture);

    public static string DayNumberPadded(DateTime value) =>
        value.Day.ToString("00", CultureInfo.InvariantCulture);

    public static string MonthShort(DateTime value) => value.ToString("MMM", CultureInfo.CurrentCulture);

    private static string UnitLabel(string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "mg" or "мг" => UiTextCatalog.Get("Nutrition_Unit_Milligrams"),
        "mcg" or "µg" or "ug" or "мкг" => UiTextCatalog.Get("Nutrition_Unit_Micrograms"),
        "g" or "г" => UiTextCatalog.Get("Nutrition_Unit_Grams"),
        _ => unit
    };
}
