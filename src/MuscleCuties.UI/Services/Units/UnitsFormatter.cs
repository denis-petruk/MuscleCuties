using MuscleCuties.Core.Domain.Profile;

namespace MuscleCuties.UI.Services.Units;

/// <summary>
/// F06: the presentation seam for the saved measurement system. Conversions stay in Core
/// (<see cref="MeasurementUnits"/>); this type adds the localized unit label so a view model can
/// format a value without importing Core.Domain or hardcoding a unit symbol.
/// </summary>
public static class UnitsFormatter
{
    /// <summary>The weight in the unit system's unit.</summary>
    public static double ToDisplayWeight(double kilograms, bool useMetricSystem) =>
        MeasurementUnits.ToDisplayWeight(kilograms, useMetricSystem);

    /// <summary>The height in the unit system's unit.</summary>
    public static double ToDisplayHeight(double centimetres, bool useMetricSystem) =>
        MeasurementUnits.ToDisplayHeight(centimetres, useMetricSystem);

    /// <summary>The distance in the unit system's unit.</summary>
    public static double ToDisplayDistance(double kilometres, bool useMetricSystem) =>
        MeasurementUnits.ToDisplayDistance(kilometres, useMetricSystem);

    /// <summary>The weight in kilograms from the displayed value.</summary>
    public static double FromDisplayWeight(double value, bool useMetricSystem) =>
        MeasurementUnits.FromDisplayWeight(value, useMetricSystem);

    /// <summary>The height in centimetres from the displayed value.</summary>
    public static double FromDisplayHeight(double value, bool useMetricSystem) =>
        MeasurementUnits.FromDisplayHeight(value, useMetricSystem);

    /// <summary>The pace converted to seconds per mile.</summary>
    public static double SecondsPerKilometreToSecondsPerMile(double secondsPerKilometre) =>
        MeasurementUnits.SecondsPerKilometreToSecondsPerMile(secondsPerKilometre);

    /// <summary>The weight unit's label.</summary>
    public static string WeightUnitLabel(bool useMetricSystem) =>
        UiTextCatalog.Get(useMetricSystem ? "Common_Unit_Kg" : "Common_Unit_Lb");

    /// <summary>The height unit's label.</summary>
    public static string HeightUnitLabel(bool useMetricSystem) =>
        UiTextCatalog.Get(useMetricSystem ? "Common_Unit_Cm" : "Common_Unit_In");

    /// <summary>The distance unit's label.</summary>
    public static string DistanceUnitLabel(bool useMetricSystem) =>
        UiTextCatalog.Get(useMetricSystem ? "Common_Unit_Km" : "Common_Unit_Mi");

    /// <summary>The pace unit's label.</summary>
    public static string PaceUnitLabel(bool useMetricSystem) => $"/{DistanceUnitLabel(useMetricSystem)}";
}
