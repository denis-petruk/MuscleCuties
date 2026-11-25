namespace MuscleCuties.Core.Domain.Profile;

/// <summary>The unit system she sees, kept separate from the stored metric values.</summary>
public sealed record DisplayUnits(
    bool UseMetricSystem,
    string BodyWeightUnit,
    string FoodMassUnit,
    string HeightUnit,
    string DistanceUnit,
    string EnergyUnit)
{
    private const string KilogramUnit = "kg";
    private const string PoundUnit = "lb";
    private const string StoneUnit = "stone";
    private const string GramUnit = "g";
    private const string OunceUnit = "oz";
    private const string CentimeterUnit = "cm";
    private const string InchUnit = "in";
    private const string KilometerUnit = "km";
    private const string MileUnit = "mi";
    private const string KilocalorieUnit = "kcal";

    /// <summary>Which system is in force, defaulting to metric when nothing was chosen.</summary>
    public string UnitSystem => UseMetricSystem ? "Metric" : "Imperial";

    /// <summary>Whether a stored value names a system this build knows.</summary>
    public bool IsSupported =>
        IsOneOf(BodyWeightUnit, KilogramUnit, PoundUnit, StoneUnit) &&
        IsOneOf(FoodMassUnit, GramUnit, OunceUnit) &&
        IsOneOf(HeightUnit, CentimeterUnit, InchUnit) &&
        IsOneOf(DistanceUnit, KilometerUnit, MileUnit) &&
        IsOneOf(EnergyUnit, KilocalorieUnit);

    /// <summary>Reads the units a profile stored, falling back to defaults for unknown values.</summary>
    public static DisplayUnits FromStored(
        string? unitSystem,
        string? bodyWeightUnit,
        string? foodMassUnit,
        string? heightUnit,
        string? distanceUnit,
        string? energyUnit)
    {
        var metric = !string.Equals(unitSystem, "Imperial", StringComparison.OrdinalIgnoreCase);
        return new DisplayUnits(
            metric,
            StoredOrDefault(bodyWeightUnit, metric ? KilogramUnit : PoundUnit),
            StoredOrDefault(foodMassUnit, metric ? GramUnit : OunceUnit),
            StoredOrDefault(heightUnit, metric ? CentimeterUnit : InchUnit),
            StoredOrDefault(distanceUnit, metric ? KilometerUnit : MileUnit),
            StoredOrDefault(energyUnit, KilocalorieUnit));
    }

    /// <summary>Reads units posted by a client and rejects values outside the supported set.</summary>
    public static DisplayUnits FromInput(
        bool useMetricSystem,
        string? bodyWeightUnit,
        string? foodMassUnit,
        string? heightUnit,
        string? distanceUnit,
        string? energyUnit)
    {
        return new DisplayUnits(
            useMetricSystem,
            TrimmedOrDefault(bodyWeightUnit, useMetricSystem ? KilogramUnit : PoundUnit),
            TrimmedOrDefault(foodMassUnit, useMetricSystem ? GramUnit : OunceUnit),
            TrimmedOrDefault(heightUnit, useMetricSystem ? CentimeterUnit : InchUnit),
            TrimmedOrDefault(distanceUnit, useMetricSystem ? KilometerUnit : MileUnit),
            TrimmedOrDefault(energyUnit, KilocalorieUnit));
    }

    /// <summary>The system to store for a requested value, falling back to the default for an unknown one.</summary>
    private static string StoredOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>The stored form of a requested value, trimmed and defaulted.</summary>
    private static string TrimmedOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>True when the value is one of the supported unit codes.</summary>
    private static bool IsOneOf(string value, params string[] supportedValues) =>
        supportedValues.Contains(value, StringComparer.OrdinalIgnoreCase);
}
