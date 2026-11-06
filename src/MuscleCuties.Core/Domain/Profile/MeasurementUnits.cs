using System.Globalization;

namespace MuscleCuties.Core.Domain.Profile;

/// <summary>
/// Canonical measurements stay in centimetres, kilograms and kilometres. A display preference
/// only changes the value and label shown to the user, never the stored value: these helpers
/// convert one way for display and back on save so the round trip keeps the canonical unit.
/// Formatting lives here so a presentation model never embeds a conversion factor or a unit
/// symbol of its own.
/// </summary>
public static class MeasurementUnits
{
    public const double KilogramsPerPound = 0.45359237;
    public const double CentimetresPerInch = 2.54;
    public const double KilometresPerMile = 1.609344;

    public static double KilogramsToPounds(double kilograms) => kilograms / KilogramsPerPound;

    public static double PoundsToKilograms(double pounds) => pounds * KilogramsPerPound;

    public static double CentimetresToInches(double centimetres) => centimetres / CentimetresPerInch;

    public static double InchesToCentimetres(double inches) => inches * CentimetresPerInch;

    public static double KilometresToMiles(double kilometres) => kilometres / KilometresPerMile;

    public static double MilesToKilometres(double miles) => miles * KilometresPerMile;

    public static double SecondsPerKilometreToSecondsPerMile(double secondsPerKilometre) =>
        secondsPerKilometre * KilometresPerMile;

    public static double ToDisplayWeight(double kilograms, bool useMetricSystem) =>
        useMetricSystem ? kilograms : KilogramsToPounds(kilograms);

    public static double ToDisplayHeight(double centimetres, bool useMetricSystem) =>
        useMetricSystem ? centimetres : CentimetresToInches(centimetres);

    public static double ToDisplayDistance(double kilometres, bool useMetricSystem) =>
        useMetricSystem ? kilometres : KilometresToMiles(kilometres);

    public static double FromDisplayWeight(double value, bool useMetricSystem) =>
        useMetricSystem ? value : PoundsToKilograms(value);

    public static double FromDisplayHeight(double value, bool useMetricSystem) =>
        useMetricSystem ? value : InchesToCentimetres(value);

    public static string WeightUnit(bool useMetricSystem) => useMetricSystem ? "kg" : "lb";

    public static string HeightUnit(bool useMetricSystem) => useMetricSystem ? "cm" : "in";

    public static string DistanceUnit(bool useMetricSystem) => useMetricSystem ? "km" : "mi";

    public static string PaceUnit(bool useMetricSystem) => useMetricSystem ? "/km" : "/mi";

    public static string FormatWeight(
        double kilograms,
        bool useMetricSystem,
        IFormatProvider? provider = null) =>
        $"{ToDisplayWeight(kilograms, useMetricSystem).ToString("N1", provider ?? CultureInfo.CurrentCulture)} " +
        WeightUnit(useMetricSystem);

    public static string FormatDistance(
        double kilometres,
        bool useMetricSystem,
        IFormatProvider? provider = null) =>
        $"{ToDisplayDistance(kilometres, useMetricSystem).ToString("N1", provider ?? CultureInfo.CurrentCulture)} " +
        DistanceUnit(useMetricSystem);

    /// <summary>Formats seconds per kilometre as m:ss in the user's selected distance system.</summary>
    public static string FormatPace(
        double secondsPerKilometre,
        bool useMetricSystem,
        IFormatProvider? provider = null)
    {
        var displaySeconds = useMetricSystem
            ? secondsPerKilometre
            : SecondsPerKilometreToSecondsPerMile(secondsPerKilometre);
        var rounded = (int)Math.Round(displaySeconds, MidpointRounding.AwayFromZero);
        var minutes = rounded / 60;
        var seconds = rounded % 60;
        return $"{minutes.ToString(provider ?? CultureInfo.CurrentCulture)}:{seconds:D2} " +
               PaceUnit(useMetricSystem);
    }
}
