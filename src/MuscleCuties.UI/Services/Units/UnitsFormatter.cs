using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Services.Units;

/// <summary>
/// F06: the presentation seam for the saved measurement system. Conversions stay in Core
/// (<see cref="MeasurementUnits"/>); this type adds the localized unit label so a view model can
/// format a value without importing Core.Domain or hardcoding a unit symbol.
/// </summary>
public static class UnitsFormatter
{
    public static double ToDisplayWeight(double kilograms, bool useMetricSystem) =>
        MeasurementUnits.ToDisplayWeight(kilograms, useMetricSystem);

    public static double ToDisplayHeight(double centimetres, bool useMetricSystem) =>
        MeasurementUnits.ToDisplayHeight(centimetres, useMetricSystem);

    public static double ToDisplayDistance(double kilometres, bool useMetricSystem) =>
        MeasurementUnits.ToDisplayDistance(kilometres, useMetricSystem);

    public static double FromDisplayWeight(double value, bool useMetricSystem) =>
        MeasurementUnits.FromDisplayWeight(value, useMetricSystem);

    public static double FromDisplayHeight(double value, bool useMetricSystem) =>
        MeasurementUnits.FromDisplayHeight(value, useMetricSystem);

    public static double SecondsPerKilometreToSecondsPerMile(double secondsPerKilometre) =>
        MeasurementUnits.SecondsPerKilometreToSecondsPerMile(secondsPerKilometre);

    public static string WeightUnitLabel(bool useMetricSystem) =>
        UiTextCatalog.Get(useMetricSystem ? "Common_Unit_Kg" : "Common_Unit_Lb");

    public static string HeightUnitLabel(bool useMetricSystem) =>
        UiTextCatalog.Get(useMetricSystem ? "Common_Unit_Cm" : "Common_Unit_In");

    public static string DistanceUnitLabel(bool useMetricSystem) =>
        UiTextCatalog.Get(useMetricSystem ? "Common_Unit_Km" : "Common_Unit_Mi");

    public static string PaceUnitLabel(bool useMetricSystem) => $"/{DistanceUnitLabel(useMetricSystem)}";
}
