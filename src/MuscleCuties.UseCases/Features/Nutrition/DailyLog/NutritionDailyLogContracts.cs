using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

/// <summary>One ingredient inside a logged food, with the amount used.</summary>
public sealed record LoggedIngredientDto(
    int FoodItemId,
    string Name,
    float Grams
);

/// <summary>One logged food with its nutrients for the amount eaten.</summary>
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
    CyclePhase? AcceptedSuggestionPhase = null,
    int? LoggedMealId = null
)
{
    /// <summary>True when the entry is a meal made of ingredients.</summary>
    public bool IsGroupedMeal => Ingredients.Count > 0;
}

/// <summary>A day as the log screen shows it: its foods, its totals and its targets.</summary>
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

/// <summary>A food remembered for quick adding, with the amount last used.</summary>
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

/// <summary>A food returned by the fast lookup, trimmed for the search list.</summary>
public sealed record FastLookupFoodItemDto(
    string Name,
    double Calories,
    double Protein,
    double Carbs,
    double Fats,
    bool IsCustom
);
