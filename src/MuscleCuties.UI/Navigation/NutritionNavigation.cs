using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UI.Navigation;

/// <summary>
/// The single place that knows how Nutrition reaches the food diary. Callers hand over meaning
/// — a logged record, or a meal and a time — instead of assembling route strings, and the diary
/// reads the same contract back out of its navigation query. Moving the diary therefore means
/// editing this file, not hunting for route literals across view models and pages.
/// </summary>
public static class NutritionNavigation
{
    public const string FoodLogRoute = "FoodLogPage";

    private const string EntryIdKey = "entryId";
    private const string AddMealKey = "addMeal";
    private const string AddMinuteOfDayKey = "addAt";

    public static string ToLogEntry(Guid historyId) => $"{FoodLogRoute}?{EntryIdKey}={historyId}";

    public static string ToAddFood(MealType mealType, int minuteOfDay) =>
        $"{FoodLogRoute}?{AddMealKey}={mealType}&{AddMinuteOfDayKey}={minuteOfDay}";

    public static bool TryReadEntryId(IDictionary<string, object> query, out Guid historyId)
    {
        historyId = Guid.Empty;
        return query.TryGetValue(EntryIdKey, out var value) &&
               Guid.TryParse(value?.ToString(), out historyId);
    }

    /// <summary>
    /// Reads the add-food intent without deciding whether it is valid: the diary's view model
    /// owns that, so a renamed or retired meal category degrades to "no sheet" instead of a
    /// navigation crash.
    /// </summary>
    public static bool TryReadAddFoodIntent(
        IDictionary<string, object> query,
        out string mealTypeName,
        out int? minuteOfDay)
    {
        mealTypeName = string.Empty;
        minuteOfDay = null;

        if (!query.TryGetValue(AddMealKey, out var mealValue))
            return false;

        var name = mealValue?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (query.TryGetValue(AddMinuteOfDayKey, out var minuteValue) &&
            int.TryParse(minuteValue?.ToString(), out var minutes))
        {
            minuteOfDay = minutes;
        }

        mealTypeName = name;
        return true;
    }
}
