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

    /// <summary>Pounds for a mass given in kilograms.</summary>
    public static double KilogramsToPounds(double kilograms) => kilograms / KilogramsPerPound;

    /// <summary>Kilograms for a mass given in pounds.</summary>
    public static double PoundsToKilograms(double pounds) => pounds * KilogramsPerPound;

    /// <summary>Inches for a length given in centimetres.</summary>
    public static double CentimetresToInches(double centimetres) => centimetres / CentimetresPerInch;

    /// <summary>Centimetres for a length given in inches.</summary>
    public static double InchesToCentimetres(double inches) => inches * CentimetresPerInch;

    /// <summary>Miles for a distance given in kilometres.</summary>
    public static double KilometresToMiles(double kilometres) => kilometres / KilometresPerMile;

    /// <summary>Kilometres for a distance given in miles.</summary>
    public static double MilesToKilometres(double miles) => miles * KilometresPerMile;

    /// <summary>Pace per mile for a pace expressed per kilometre.</summary>
    public static double SecondsPerKilometreToSecondsPerMile(double secondsPerKilometre) =>
        secondsPerKilometre * KilometresPerMile;

    /// <summary>Weight in the units the profile displays.</summary>
    public static double ToDisplayWeight(double kilograms, bool useMetricSystem) =>
        useMetricSystem ? kilograms : KilogramsToPounds(kilograms);

    /// <summary>Height in the units the profile displays.</summary>
    public static double ToDisplayHeight(double centimetres, bool useMetricSystem) =>
        useMetricSystem ? centimetres : CentimetresToInches(centimetres);

    /// <summary>Distance in the units the profile displays.</summary>
    public static double ToDisplayDistance(double kilometres, bool useMetricSystem) =>
        useMetricSystem ? kilometres : KilometresToMiles(kilometres);

    /// <summary>Kilograms for a weight the user entered in display units.</summary>
    public static double FromDisplayWeight(double value, bool useMetricSystem) =>
        useMetricSystem ? value : PoundsToKilograms(value);

    /// <summary>Centimetres for a height the user entered in display units.</summary>
    public static double FromDisplayHeight(double value, bool useMetricSystem) =>
        useMetricSystem ? value : InchesToCentimetres(value);

    /// <summary>Label for the weight unit in use.</summary>
    public static string WeightUnit(bool useMetricSystem) => useMetricSystem ? "kg" : "lb";

    /// <summary>Label for the height unit in use.</summary>
    public static string HeightUnit(bool useMetricSystem) => useMetricSystem ? "cm" : "in";

    /// <summary>Label for the distance unit in use.</summary>
    public static string DistanceUnit(bool useMetricSystem) => useMetricSystem ? "km" : "mi";

    /// <summary>Label for the pace unit in use.</summary>
    public static string PaceUnit(bool useMetricSystem) => useMetricSystem ? "/km" : "/mi";

    /// <summary>Formats a weight with its unit for display.</summary>
    public static string FormatWeight(
        double kilograms,
        bool useMetricSystem,
        IFormatProvider? provider = null) =>
        $"{ToDisplayWeight(kilograms, useMetricSystem).ToString("N1", provider ?? CultureInfo.CurrentCulture)} " +
        WeightUnit(useMetricSystem);

    /// <summary>Formats a distance with its unit for display.</summary>
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
