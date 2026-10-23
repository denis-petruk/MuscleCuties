using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Nutrition;

public sealed record FoodItemEntryDto(
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
    DateTime LoggedAtTimestamp)
{
    public IReadOnlyList<HistoryIngredientDto> Ingredients { get; init; } = [];
    public string? AcceptedSuggestionName { get; init; }
    public CyclePhase? AcceptedSuggestionPhase { get; init; }
    public bool IsGroupedMeal => Ingredients.Count > 0;
}

public sealed record HistoryIngredientDto(int FoodItemId, string Name, float Grams);

public sealed record DailyNutritionLogDto(
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
    IReadOnlyList<FoodItemEntryDto> Entries);

public sealed record DailyNutritionSummaryDto(
    DateOnly Date,
    Guid LogId,
    double TotalCalories,
    double TotalProtein,
    double TotalCarbs,
    double TotalFat,
    double TargetCalories,
    int WaterIntakeMl,
    int ItemsCount);

public sealed record QuickFoodItemDto(
    string Name,
    double ServingSize,
    string ServingUnit,
    double ServingsCount,
    double Calories,
    double Protein,
    double Carbs,
    double Fat,
    string Detail)
{
    // History references are always re-authorized by the facade before re-logging.
    public Guid? SourceId { get; init; }
    public int? IngredientFoodItemId { get; init; }
    public IReadOnlyList<HistoryIngredientDto> Ingredients { get; init; } = [];
}
