using MuscleCuties.Core.Domain.Progress;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.UI.Models.UI.Common;

/// <summary>One logged body measurement, formatted for the history list.</summary>
public sealed record MeasurementHistoryRow(BodyMeasurementOutputDto Reading)
{
    /// <summary>Site label with its side when the site is one of the paired limbs.</summary>
    public string Name
    {
        get
        {
            var site = UiTextCatalog.Get($"Profile_Measurement_Site_{Reading.Site}");
            return Reading.Side == BodyMeasurementCodes.NoSide
                ? site
                : UiTextCatalog.Format("Profile_Measurement_SiteAndSide", site,
                    UiTextCatalog.Get($"Profile_Measurement_Side_{Reading.Side}"));
        }
    }

    /// <summary>date, value and unit as one line, so the row needs no second text slot.</summary>
    public string Detail => UiTextCatalog.Format("Profile_Measurement_HistoryDetail",
        Reading.LocalDate.ToString("d", CultureInfo.CurrentCulture),
        Reading.EnteredValue.ToString("0.##", CultureInfo.CurrentCulture),
        UiTextCatalog.Get(Reading.EnteredUnit == BodyMeasurementCodes.Inches ? "Common_Unit_In" : "Common_Unit_Cm"));

    /// <summary>Announcement for the row's edit action, naming the reading it reopens.</summary>
    public string EditDescription => UiTextCatalog.Format("Profile_Measurement_EditReading", Name, Detail);
}

/// <summary>The measurement site, side and unit choices the editor offers, and the parsing it applies</summary>
/// before a draft becomes a real <see cref="BodyMeasurement" />.</summary>
internal static class BodyMeasurementPresentation
{
    public const string NoSide = BodyMeasurementCodes.NoSide;

    /// <summary>Every measurable site as its persisted code plus the resource key for its label.</summary>
    public static IReadOnlyList<SelectionOption<string>> Sites { get; } = Enum.GetValues<BodyMeasurementSite>()
        .Select(site =>
        {
            var code = BodyMeasurementCodes.SiteCode(site);
            return new SelectionOption<string>(code, $"Profile_Measurement_Site_{code}");
        })
        .ToArray();

    /// <summary>Left and right, offered only for sites that <see cref="NeedsSide" /> reports.</summary>
    public static IReadOnlyList<SelectionOption<string>> Sides { get; } =
    [
        new(BodyMeasurementCodes.Left, "Profile_Measurement_Side_left"),
        new(BodyMeasurementCodes.Right, "Profile_Measurement_Side_right")
    ];

    /// <summary>Centimetres and inches, in the order the picker shows them.</summary>
    public static IReadOnlyList<SelectionOption<string>> Units { get; } =
    [
        new(BodyMeasurementCodes.Centimeters, "Common_Unit_Cm"),
        new(BodyMeasurementCodes.Inches, "Common_Unit_In")
    ];

    /// <summary>Only the paired limbs are recorded per side; waist and chest are not.</summary>
    public static bool NeedsSide(string? site) => site is
        BodyMeasurementCodes.UpperArm or BodyMeasurementCodes.Thigh or BodyMeasurementCodes.Calf;

    /// <summary>Picks the unit to preselect, falling back to the person's measurement system</summary>
    /// when the stored unit is missing or no longer offered.</summary>
    public static SelectionOption<string> FindUnit(string? enteredUnit, bool preferMetric)
    {
        var defaultUnit = preferMetric ? BodyMeasurementCodes.Centimeters : BodyMeasurementCodes.Inches;
        return Units.FirstOrDefault(option => option.Value == (enteredUnit ?? defaultUnit))
            ?? Units.Single(option => option.Value == defaultUnit);
    }

    /// <summary>Accepts a typed reading only if it parses and the domain accepts it, so the editor</summary>
    /// never saves a value the model would later reject.</summary>
    public static bool TryParseDraft(string input, string? site, string? side, string? unit,
        DateOnly date, Guid id, out double value)
    {
        value = 0;
        if (site is null || side is null || unit is null ||
            !(decimal.TryParse(input, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                  CultureInfo.CurrentCulture, out var entered) ||
              decimal.TryParse(input, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                  CultureInfo.InvariantCulture, out entered)) ||
            decimal.Round(entered, BodyMeasurement.MaximumInputDecimalPlaces) != entered)
            return false;

        value = (double)entered;
        try
        {
            _ = BodyMeasurement.Create(id, BodyMeasurementCodes.ParseSite(site), BodyMeasurementCodes.ParseSide(side),
                date, value, BodyMeasurementCodes.ParseUnit(unit), DateOnly.FromDateTime(DateTime.UtcNow));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
