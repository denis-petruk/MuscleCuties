using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

public sealed record LoggedIngredientDto(
    int FoodItemId,
    string Name,
    float Grams
);

public sealed record LoggedFoodItemDto(
    Guid Id,
    Guid DailyLogId,
    MealType MealType,
    string Name,
    double ServingSize,
    string ServingUnit,
    double ServingsCount,
    double Calories,
    double Protein,
    double Carbs,
    double Fat,
    DateTime LoggedAtTimestamp,
    IReadOnlyList<LoggedIngredientDto> Ingredients,
    string? AcceptedSuggestionName = null,
    CyclePhase? AcceptedSuggestionPhase = null
)
{
    public bool IsGroupedMeal => Ingredients.Count > 0;
}

public sealed record DailyNutritionLogSnapshot(
    Guid Id,
    DateOnly Date,
    int WaterIntakeMl,
    double TargetCalories,
    double TargetProtein,
    double TargetCarbs,
    double TargetFat,
    double TotalCalories,
    double TotalProtein,
    double TotalCarbs,
    double TotalFat,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<LoggedFoodItemDto> Entries
);

public sealed record DailyNutritionSummarySnapshot(
    DateOnly Date,
    Guid LogId,
    double TotalCalories,
    double TotalProtein,
    double TotalCarbs,
    double TotalFat,
    double TargetCalories,
    int WaterIntakeMl,
    int ItemsCount
);

public sealed record QuickFoodItemSnapshot(
    string Name,
    double ServingSize,
    string ServingUnit,
    double ServingsCount,
    double Calories,
    double Protein,
    double Carbs,
    double Fat,
    string Detail,
    Guid? SourceId = null,
    int? IngredientFoodItemId = null,
    IReadOnlyList<LoggedIngredientDto>? Ingredients = null
);

public sealed record FastLookupFoodItemDto(
    string Name,
    double Calories,
    double Protein,
    double Carbs,
    double Fats,
    bool IsCustom
);

public sealed record SaveFoodItemRequest(
    DateOnly Date,
    LoggedFoodItemDto Item,
    bool RequireExisting = false
);

public sealed record ReLogFoodRequest(
    DateOnly Date,
    MealType MealType,
    QuickFoodItemSnapshot QuickItem,
    LoggedFoodItemDto Draft
);
