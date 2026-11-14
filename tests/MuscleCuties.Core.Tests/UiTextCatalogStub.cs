namespace MuscleCuties.UI.Services.Localization;

internal static class UiTextCatalog
{
    public static string Get(string key) => key switch
    {
        "Dashboard_CheckIn_HealthKitSleepBadge" => "Auto-filled from Apple Health ({0}h)",
        "Dashboard_CheckIn_SuggestFromAppleHealth" => "Suggest ratings from Apple Health",
        _ => key
    };

    public static string Format(string key, params object?[] args) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), args);
}
