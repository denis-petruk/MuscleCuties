using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Hydration.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;

namespace MuscleCuties.UI.Services.Nutrition;

public static partial class NutritionPresentationMapper
{
    public static FoodLogEntry FromLoggedFood(LoggedFoodItemDto item) =>
        new(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp)
        {
            Ingredients = item.Ingredients.Select(i => new FoodHistoryIngredient(i.FoodItemId, i.Name, i.Grams)).ToList(),
            AcceptedSuggestionName = item.AcceptedSuggestionName,
            AcceptedSuggestionPhase = item.AcceptedSuggestionPhase,
            LoggedMealId = item.LoggedMealId
        };

    public static LoggedFoodItemDto ToLoggedFood(FoodLogEntry item) =>
        new(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            item.AcceptedSuggestionName, item.AcceptedSuggestionPhase, item.LoggedMealId);

    public static LoggedFoodItemDto ToLoggedFood(LoggedFoodEntry item) =>
        new(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            item.AcceptedSuggestionName, item.AcceptedSuggestionPhase, item.LoggedMealId);

    public static IReadOnlyList<LoggedFoodItemDto> ToLoggedFoods(IEnumerable<LoggedFoodEntry> items) =>
        items.Select(ToLoggedFood).ToList();

    public static QuickFood FromQuickSnapshot(QuickFoodItemSnapshot item) =>
        new(item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories, item.Protein,
            item.Carbs, item.Fat, item.Detail)
        {
            SourceId = item.SourceId,
            IngredientFoodItemId = item.IngredientFoodItemId,
            Ingredients = item.Ingredients?.Select(i => new FoodHistoryIngredient(i.FoodItemId, i.Name, i.Grams)).ToList() ?? []
        };

    public static QuickFoodItemSnapshot ToQuickSnapshot(QuickFood item) =>
        new(item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories, item.Protein,
            item.Carbs, item.Fat, item.Detail, item.SourceId, item.IngredientFoodItemId,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList());

    public static QuickFood FromFastLookup(FastLookupFoodItemDto food) =>
        new(food.Name, 100, "g", 1, food.Calories, food.Protein, food.Carbs, food.Fats,
            food.IsCustom ? "Custom food · per 100 g" : "Per 100 g");

    public static WaterQuickAddDefaults FromWaterQuickAddDefaults(WaterQuickAddDefaultsDto item) =>
        new(item.VolumesMillilitres);
}
