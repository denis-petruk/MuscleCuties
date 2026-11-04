namespace MuscleCuties.Core.Domain.Progress;

/// <summary>Stable wire codes for the approved first-build tape vocabulary.</summary>
public static class BodyMeasurementCodes
{
    public const string Waist = "waist";
    public const string Hips = "hips";
    public const string Chest = "chest";
    public const string UpperArm = "upper-arm";
    public const string Thigh = "thigh";
    public const string Calf = "calf";
    public const string NoSide = "none";
    public const string Left = "left";
    public const string Right = "right";
    public const string Centimeters = "cm";
    public const string Inches = "in";

    public static BodyMeasurementSite ParseSite(string? code) => code switch
    {
        Waist => BodyMeasurementSite.Waist,
        Hips => BodyMeasurementSite.Hips,
        Chest => BodyMeasurementSite.Chest,
        UpperArm => BodyMeasurementSite.UpperArm,
        Thigh => BodyMeasurementSite.Thigh,
        Calf => BodyMeasurementSite.Calf,
        _ => throw new ArgumentOutOfRangeException(nameof(code), "Unknown measurement site.")
    };

    public static BodyMeasurementSide ParseSide(string? code) => code switch
    {
        NoSide => BodyMeasurementSide.None,
        Left => BodyMeasurementSide.Left,
        Right => BodyMeasurementSide.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(code), "Unknown measurement side.")
    };

    public static BodyMeasurementUnit ParseUnit(string? code) => code switch
    {
        Centimeters => BodyMeasurementUnit.Centimeters,
        Inches => BodyMeasurementUnit.Inches,
        _ => throw new ArgumentOutOfRangeException(nameof(code), "Unknown measurement unit.")
    };

    public static string SiteCode(BodyMeasurementSite site) => site switch
    {
        BodyMeasurementSite.Waist => Waist,
        BodyMeasurementSite.Hips => Hips,
        BodyMeasurementSite.Chest => Chest,
        BodyMeasurementSite.UpperArm => UpperArm,
        BodyMeasurementSite.Thigh => Thigh,
        BodyMeasurementSite.Calf => Calf,
        _ => throw new ArgumentOutOfRangeException(nameof(site))
    };

    public static string SideCode(BodyMeasurementSide side) => side switch
    {
        BodyMeasurementSide.None => NoSide,
        BodyMeasurementSide.Left => Left,
        BodyMeasurementSide.Right => Right,
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    public static string UnitCode(BodyMeasurementUnit unit) => unit switch
    {
        BodyMeasurementUnit.Centimeters => Centimeters,
        BodyMeasurementUnit.Inches => Inches,
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
}
