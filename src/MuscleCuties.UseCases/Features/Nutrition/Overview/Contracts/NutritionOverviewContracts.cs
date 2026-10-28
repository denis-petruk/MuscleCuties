namespace MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

public sealed record NutritionOverviewRequest(
    DateOnly Date,
    string BreakfastPreference = "Savoury"
);

public sealed record NutritionTargetMacrosDto(
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    NutritionMealDistributionDto? MealDistribution = null
);

public sealed record NutritionMealDistributionDto(float Breakfast, float Lunch, float Dinner, float Snack);

public sealed record NutritionConsumedMacrosDto(
    float Calories,
    float Protein,
    float Carbs,
    float Fats
);

public sealed record MacroBreakdownItemDto(
    string Name,
    float Grams,
    float Calories,
    float Progress
);

public sealed record DailyMicronutrientDto(
    string Group,
    string Name,
    string Unit,
    float Amount,
    float Goal
);

public sealed record MealSummaryDto(
    Guid HistoryId,
    int LoggedMealId,
    string Time,
    string MealType,
    string Name,
    string CaloriesText,
    string MacrosText,
    string FiberText,
    string NutrientSummaryText,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    IReadOnlyList<MacroBreakdownItemDto> MacroItems,
    IReadOnlyList<DailyMicronutrientDto> Micronutrients
);

/// <summary>Ingredient amount in grams and nutrient densities per 100 grams.</summary>
public sealed record MealSuggestionComponentDto(
    int FoodItemId,
    string Name,
    string ComponentType,
    string Role,
    float Grams,
    bool IsLocked,
    float Calories,
    float Protein,
    float Carbs,
    float Fats
);

public sealed record MealSuggestionComponentGroupDto(
    string ComponentType,
    string Title,
    IReadOnlyList<MealSuggestionComponentDto> Items
);

public sealed record MealSuggestionDto(
    string ConceptName,
    string ConceptDescription,
    IReadOnlyList<string> SpiceBlendNames,
    IReadOnlyList<MealSuggestionComponentDto> Components,
    IReadOnlyList<MealSuggestionComponentGroupDto> ComponentGroups,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float Score,
    string Style,
    string IconGlyph,
    string DirectionsText
);

public sealed record DayMealPlanItemDto(
    string MealType,
    float TargetCalories,
    float TargetProtein,
    float TargetCarbs,
    float TargetFats,
    float DailyCaloriesTarget,
    bool IsAlreadyLogged,
    MealSuggestionDto? Suggestion,
    string ErrorMessage = ""
);

public sealed record MealIngredientInputDto(
    int FoodItemId,
    string Name,
    float Grams,
    float Amount,
    string ServingLabel,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    string SourceSummary
);

public sealed record LogMealRequest(
    DateOnly Date,
    string MealType,
    TimeSpan MealTime,
    IReadOnlyList<MealIngredientInputDto> Ingredients,
    string? AcceptedSuggestionName = null,
    string? AcceptedSuggestionPhase = null
);

public sealed record UpdateMealRequest(
    int MealId,
    DateOnly Date,
    string MealType,
    TimeSpan MealTime,
    IReadOnlyList<MealIngredientInputDto> Ingredients
);

public sealed record CreateCustomFoodRequest(
    string Name,
    float ServingAmount,
    string ServingUnit,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    string? CompanySource = null,
    float? IronMilligrams = null,
    float? VitaminB12Micrograms = null,
    float? VitaminDMicrograms = null,
    float? CalciumMilligrams = null
);

public sealed record CustomFoodResultDto(
    int FoodItemId,
    string Name,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    string SourceSummary,
    float? ServingSize,
    string? ServingSizeUnit,
    string? ServingOptionsJson,
    string NutritionSummary
);

public sealed record NutritionTargetBreakdownDto(
    string Source,
    float? StartingEnergyCalories,
    float? GoalAdjustmentCalories,
    float? PhaseAdjustmentCalories,
    float? CalculatedCalories,
    float? SavedRequestedCalories,
    float ShownCalories,
    bool HasSavedMacroOverride,
    bool SavedRequestDiffersFromShownTarget
);

public sealed record NutritionOverviewResponse(
    NutritionTargetMacrosDto Targets,
    NutritionConsumedMacrosDto Consumed,
    string CurrentPhase,
    string CurrentPhaseName,
    string PhaseFocusTitle,
    string PhaseFocusCopy,
    IReadOnlyList<MealSummaryDto> Meals,
    IReadOnlyList<DailyMicronutrientDto> Micronutrients,
    string BreakfastPreference,
    string PhaseAvailability = "Available",
    string TargetSource = "Account targets",
    NutritionTargetBreakdownDto? TargetBreakdown = null
);
