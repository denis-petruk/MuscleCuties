namespace MuscleCuties.UI.Navigation;

/// <summary>
/// The single place that knows how Nutrition reaches the food diary. Callers hand over a logged
/// record instead of assembling route strings, and the diary reads the same contract back out of
/// its navigation query. Moving the diary therefore means editing this file, not hunting for route
/// literals across view models and pages.
/// </summary>
public static class NutritionNavigation
{
    public const string FoodLogRoute = "FoodLogPage";

    private const string EntryIdKey = "entryId";

    /// <summary>Route to the log entry for a history id.</summary>
    public static string ToLogEntry(Guid historyId) => $"{FoodLogRoute}?{EntryIdKey}={historyId}";

    /// <summary>Reads the diary entry id back out of the navigation query, so the diary does not</summary>
    /// have to know the key or its format.</summary>
    public static bool TryReadEntryId(IDictionary<string, object> query, out Guid historyId)
    {
        historyId = Guid.Empty;
        return query.TryGetValue(EntryIdKey, out var value) &&
               Guid.TryParse(value?.ToString(), out historyId);
    }
}
