using System.Globalization;
using MuscleCuties.Core.Domain.Progress;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.UI.Models.UI.Common;

public sealed record MeasurementHistoryRow(BodyMeasurementOutputDto Reading)
{
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

    public string Detail => UiTextCatalog.Format("Profile_Measurement_HistoryDetail",
        Reading.LocalDate.ToString("d", CultureInfo.CurrentCulture),
        Reading.EnteredValue.ToString("0.##", CultureInfo.CurrentCulture),
        UiTextCatalog.Get(Reading.EnteredUnit == BodyMeasurementCodes.Inches ? "Common_Unit_In" : "Common_Unit_Cm"));

    public string EditDescription => UiTextCatalog.Format("Profile_Measurement_EditReading", Name, Detail);
}

internal static class BodyMeasurementPresentation
{
    public const string NoSide = BodyMeasurementCodes.NoSide;

    public static IReadOnlyList<SelectionOption<string>> Sites { get; } = Enum.GetValues<BodyMeasurementSite>()
        .Select(site =>
        {
            var code = BodyMeasurementCodes.SiteCode(site);
            return new SelectionOption<string>(code, $"Profile_Measurement_Site_{code}");
        })
        .ToArray();

    public static IReadOnlyList<SelectionOption<string>> Sides { get; } =
    [
        new(BodyMeasurementCodes.Left, "Profile_Measurement_Side_left"),
        new(BodyMeasurementCodes.Right, "Profile_Measurement_Side_right")
    ];

    public static IReadOnlyList<SelectionOption<string>> Units { get; } =
    [
        new(BodyMeasurementCodes.Centimeters, "Common_Unit_Cm"),
        new(BodyMeasurementCodes.Inches, "Common_Unit_In")
    ];

    public static bool NeedsSide(string? site) => site is
        BodyMeasurementCodes.UpperArm or BodyMeasurementCodes.Thigh or BodyMeasurementCodes.Calf;

    public static SelectionOption<string> FindUnit(string? enteredUnit, bool preferMetric)
    {
        var defaultUnit = preferMetric ? BodyMeasurementCodes.Centimeters : BodyMeasurementCodes.Inches;
        return Units.FirstOrDefault(option => option.Value == (enteredUnit ?? defaultUnit))
            ?? Units.Single(option => option.Value == defaultUnit);
    }

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
