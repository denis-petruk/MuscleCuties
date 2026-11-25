using MuscleCuties.UseCases.Features.Hydration.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>Conversions from logged food and its ingredients to the entries and history rows the diary shows.</summary>
public static partial class NutritionPresentationMapper
{
    /// <summary>The logged food as a diary entry.</summary>
    public static FoodLogEntry FromLoggedFood(LoggedFoodItemDto item) =>
        new(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp)
        {
            Ingredients = item.Ingredients.Select(i => new FoodHistoryIngredient(i.FoodItemId, i.Name, i.Grams)).ToList(),
            AcceptedSuggestionName = item.AcceptedSuggestionName,
            AcceptedSuggestionPhase = item.AcceptedSuggestionPhase,
            LoggedMealId = item.LoggedMealId
        };

    /// <summary>The diary entry as a logged food request.</summary>
    public static LoggedFoodItemDto ToLoggedFood(FoodLogEntry item) =>
        new(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            item.AcceptedSuggestionName, item.AcceptedSuggestionPhase, item.LoggedMealId);

    /// <summary>The entry as a logged food request.</summary>
    public static LoggedFoodItemDto ToLoggedFood(LoggedFoodEntry item) =>
        new(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            item.AcceptedSuggestionName, item.AcceptedSuggestionPhase, item.LoggedMealId);

    /// <summary>The entries as logged food requests.</summary>
    public static IReadOnlyList<LoggedFoodItemDto> ToLoggedFoods(IEnumerable<LoggedFoodEntry> items) =>
        items.Select(ToLoggedFood).ToList();

    /// <summary>The stored snapshot as a quick food.</summary>
    public static QuickFood FromQuickSnapshot(QuickFoodItemSnapshot item) =>
        new(item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories, item.Protein,
            item.Carbs, item.Fat, item.Detail)
        {
            SourceId = item.SourceId,
            IngredientFoodItemId = item.IngredientFoodItemId,
            Ingredients = item.Ingredients?.Select(i => new FoodHistoryIngredient(i.FoodItemId, i.Name, i.Grams)).ToList() ?? []
        };

    /// <summary>The quick food as a snapshot.</summary>
    public static QuickFoodItemSnapshot ToQuickSnapshot(QuickFood item) =>
        new(item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories, item.Protein,
            item.Carbs, item.Fat, item.Detail, item.SourceId, item.IngredientFoodItemId,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList());

    /// <summary>The fast lookup result as a quick food.</summary>
    public static QuickFood FromFastLookup(FastLookupFoodItemDto food) =>
        new(food.Name, 100, "g", 1, food.Calories, food.Protein, food.Carbs, food.Fats,
            food.IsCustom ? "Custom food · per 100 g" : "Per 100 g");

    /// <summary>The water quick-add defaults.</summary>
    public static WaterQuickAddDefaults FromWaterQuickAddDefaults(WaterQuickAddDefaultsDto item) =>
        new(item.VolumesMillilitres);
}
