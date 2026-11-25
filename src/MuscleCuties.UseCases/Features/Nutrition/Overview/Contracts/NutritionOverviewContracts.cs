namespace MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

/// <summary>A request for the nutrition overview of a day.</summary>
public sealed record NutritionOverviewRequest(
    DateOnly Date,
    string BreakfastPreference = "Savoury"
);

/// <summary>The day's macro targets.</summary>
public sealed record NutritionTargetMacrosDto(
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    NutritionMealDistributionDto? MealDistribution = null
);

/// <summary>How the day's calories split across meals.</summary>
public sealed record NutritionMealDistributionDto(float Breakfast, float Lunch, float Dinner, float Snack);

/// <summary>What has been eaten, against the targets.</summary>
public sealed record NutritionConsumedMacrosDto(
    float Calories,
    float Protein,
    float Carbs,
    float Fats
);

/// <summary>One macro's share of the day, with the amount it accounts for.</summary>
public sealed record MacroBreakdownItemDto(
    string Name,
    float Grams,
    float Calories,
    float Progress
);

/// <summary>One micronutrient's total for the day.</summary>
public sealed record DailyMicronutrientDto(
    string Group,
    string Name,
    string Unit,
    float Amount,
    float Goal
);

/// <summary>
/// Additive, backward-compatible coverage metadata. <see cref="Status"/> is one of KnownZero,
/// Complete, Partial or Unavailable. It states what the micronutrient totals actually cover; it
/// never changes an amount and is absent for clients that do not read it.
/// </summary>
public sealed record MicronutrientCoverageDto(
    string Status,
    int CoveredItemCount,
    int UncoveredItemCount,
    IReadOnlyList<string> UnavailableItemNames
);

/// <summary>One meal's summary in the overview.</summary>
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
    IReadOnlyList<DailyMicronutrientDto> Micronutrients,
    string IngredientSummary = ""
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

/// <summary>One component of a suggested meal, with its ingredients.</summary>
public sealed record MealSuggestionComponentGroupDto(
    string ComponentType,
    string Title,
    IReadOnlyList<MealSuggestionComponentDto> Items
);

/// <summary>A meal the planner suggests, sized to the day's remaining targets.</summary>
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

/// <summary>One planned meal in the day's plan.</summary>
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

/// <summary>An ingredient she is adding to a meal, with its weight.</summary>
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

/// <summary>A meal to log, idempotent on its key.</summary>
public sealed record LogMealRequest(
    DateOnly Date,
    string MealType,
    TimeSpan MealTime,
    IReadOnlyList<MealIngredientInputDto> Ingredients,
    string? AcceptedSuggestionName = null,
    string? AcceptedSuggestionPhase = null
);

/// <summary>A change to a logged meal.</summary>
public sealed record UpdateMealRequest(
    int MealId,
    DateOnly Date,
    string MealType,
    TimeSpan MealTime,
    IReadOnlyList<MealIngredientInputDto> Ingredients
);

/// <summary>A food to create from inside the meal flow.</summary>
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

/// <summary>The created food, so the meal can reference it immediately.</summary>
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

/// <summary>The targets broken down by source, so the client can explain where a number came from.</summary>
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

/// <summary>The whole overview payload for one day.</summary>
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
    NutritionTargetBreakdownDto? TargetBreakdown = null,
    MicronutrientCoverageDto? MicronutrientCoverage = null
);
