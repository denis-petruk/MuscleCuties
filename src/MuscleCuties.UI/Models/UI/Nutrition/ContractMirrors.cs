using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>One logged entry as the diary binds it, including the components of a grouped meal.</summary>
public sealed record FoodLogEntry(
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
    /// <summary>Components of a grouped meal; empty for an entry that is a single food.</summary>
    public IReadOnlyList<FoodHistoryIngredient> Ingredients { get; init; } = [];
    /// <summary>The name of the suggestion the user accepted.</summary>
    public string? AcceptedSuggestionName { get; init; }
    /// <summary>The cycle phase of the accepted suggestion.</summary>
    public CyclePhase? AcceptedSuggestionPhase { get; init; }
    /// <summary>The logged meal this entry belongs to.</summary>
    public int? LoggedMealId { get; init; }
    /// <summary>True when the entry is a meal with ingredients.</summary>
    public bool IsGroupedMeal => Ingredients.Count > 0;
}

/// <summary>One component of a grouped meal: the catalog item, its name, and its weight in grams.</summary>
public sealed record FoodHistoryIngredient(int FoodItemId, string Name, float Grams);
/// <summary>The same component shape, used when a meal is submitted rather than read back.</summary>
public sealed record LoggedFoodIngredient(int FoodItemId, string Name, float Grams);

/// <summary>A food entry being submitted. It carries its components in the constructor because a grouped</summary>
/// meal is invalid without them.</summary>
public sealed record LoggedFoodEntry(
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
    IReadOnlyList<LoggedFoodIngredient> Ingredients,
    string? AcceptedSuggestionName = null,
    CyclePhase? AcceptedSuggestionPhase = null)
{
    /// <summary>The logged meal this entry belongs to.</summary>
    public int? LoggedMealId { get; init; }
    /// <summary>True when the entry is a meal with ingredients.</summary>
    public bool IsGroupedMeal => Ingredients.Count > 0;
}

/// <summary>One row in the quick-add list, with the macros it would add if chosen.</summary>
public sealed record QuickFood(
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
    /// <summary>Catalog food behind the quick add; null for an entry the person composed themselves.</summary>
    public Guid? SourceId { get; init; }
    /// <summary>Catalog item a single-component quick add refers to, when it is part of a saved meal.</summary>
    public int? IngredientFoodItemId { get; init; }

    /// <summary>Components, when the quick add replays a whole saved meal instead of one food.</summary>
    public IReadOnlyList<FoodHistoryIngredient> Ingredients { get; init; } = [];
}

/// <summary>The volumes the water quick-add buttons offer, in millilitres.</summary>
public sealed record WaterQuickAddDefaults(IReadOnlyList<int> VolumesMillilitres);

/// <summary>A catalog row: per-serving macros, its stored serving options, and the provenance and nutrition</summary>
/// summaries the picker shows.</summary>
public sealed record FoodCatalogItem(
    int Id,
    string Name,
    float? Calories,
    float? Protein,
    float? Carbs,
    float? Fats,
    float? ServingSize,
    string? ServingSizeUnit,
    string? ServingOptionsJson,
    string SourceSummary,
    string NutritionSummary,
    bool IsCustom,
    string FoodKey = "");

/// <summary>One named portion of a food and the weight it represents.</summary>
public sealed record FoodServingOption(string Label, string Unit, float Grams);

/// <summary>The gram unit, the units a portion may be entered in, and the standard portions for a food.</summary>
public sealed record FoodPortionCatalog(
    string GramUnit,
    IReadOnlyList<string> SupportedUnits,
    IReadOnlyList<FoodServingOption> StandardOptions);

/// <summary>An ingredient being added to a recipe, before the server assigns it an identity.</summary>
public sealed record RecipeIngredientInput(
    Guid? FoodItemId,
    string Name,
    float Grams,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float Fiber = 0f,
    float SugarAlcohols = 0f,
    string? SourceKind = null,
    string? SourceLabel = null,
    string? SourceVersion = null);

/// <summary>A stored recipe ingredient, including net carbs and the provenance of its nutrition values.</summary>
public sealed record RecipeIngredient(
    Guid Id,
    Guid? FoodItemId,
    string Name,
    float Grams,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float Fiber,
    float SugarAlcohols,
    float NetCarbs,
    string? SourceKind = null,
    string? SourceLabel = null,
    string? SourceVersion = null);

/// <summary>A recipe as the list shows it: per-serving totals, weight and ingredient count.</summary>
public sealed record RecipeSummaryItem(
    Guid Id,
    string Name,
    float ServingsCount,
    float PerServingCalories,
    float PerServingProtein,
    float PerServingCarbs,
    float PerServingNetCarbs,
    float PerServingFats,
    float PerServingGrams,
    int IngredientCount,
    DateTime CreatedAtUtc,
    int ConcurrencyVersion = 1);

/// <summary>A recipe with its total and per-serving values plus its ingredients, for the detail screen.</summary>
public sealed record RecipeDetailsItem(
    Guid Id,
    string Name,
    string? Description,
    float ServingsCount,
    float? CookedWeightGrams,
    float TotalRawWeightGrams,
    float TotalCalories,
    float TotalProtein,
    float TotalCarbs,
    float TotalNetCarbs,
    float TotalFats,
    float PerServingCalories,
    float PerServingProtein,
    float PerServingCarbs,
    float PerServingNetCarbs,
    float PerServingFats,
    float PerServingGrams,
    IReadOnlyList<RecipeIngredient> Ingredients,
    DateTime CreatedAtUtc,
    int ConcurrencyVersion = 1);

/// <summary>One food inside a saved meal template, with the portion it replays.</summary>
public sealed record MealTemplateItem(
    Guid Id,
    string Name,
    float ServingSize,
    string ServingUnit,
    float ServingsCount,
    float Calories,
    float Protein,
    float Carbs,
    float Fats);

/// <summary>A saved meal template and the items it adds back into the diary.</summary>
public sealed record MealTemplate(
    Guid Id,
    string Name,
    int MealType,
    float TotalCalories,
    float TotalProtein,
    float TotalCarbs,
    float TotalFats,
    int ItemCount,
    IReadOnlyList<MealTemplateItem> Items,
    DateTime CreatedAtUtc);

/// <summary>How the daily calorie target is split across the four meals.</summary>
public sealed record NutritionMealDistribution(float Breakfast, float Lunch, float Dinner, float Snack);

/// <summary>How the shown calorie target was reached: the starting energy, the goal and phase adjustments,</summary>
/// and whether a saved request overrides the calculated value.</summary>
public sealed record NutritionTargetBreakdown(
    string Source,
    float? StartingEnergyCalories,
    float? GoalAdjustmentCalories,
    float? PhaseAdjustmentCalories,
    float? CalculatedCalories,
    float? SavedRequestedCalories,
    float ShownCalories,
    bool HasSavedMacroOverride,
    bool SavedRequestDiffersFromShownTarget);

/// <summary>One micronutrient's intake today against its goal.</summary>
public sealed record DailyMicronutrient(
    string Group,
    string Name,
    string Unit,
    float Amount,
    float Goal);

/// <summary>A logged meal as the nutrition screen summarises it, with its text already formatted and its</summary>
/// macro and micronutrient breakdowns attached.</summary>
public sealed record NutritionMealSummary(
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
    IReadOnlyList<MacroBreakdownItem> MacroItems,
    IReadOnlyList<DailyMicronutrient> Micronutrients,
    string IngredientSummary = "");

/// <summary>One food inside a generated meal suggestion.</summary>
public sealed record NutritionSuggestionComponent(
    int FoodItemId,
    string Name,
    string ComponentType,
    string Role,
    float Grams,
    bool IsLocked,
    float Calories,
    float Protein,
    float Carbs,
    float Fats);

/// <summary>Suggestion components that share a heading in the generated meal card.</summary>
public sealed record NutritionSuggestionComponentGroup(
    string ComponentType,
    string Title,
    IReadOnlyList<NutritionSuggestionComponent> Items);

/// <summary>A generated meal: its components, macros, style and the directions built from those components.</summary>
public sealed record NutritionMealSuggestion(
    string ConceptName,
    string ConceptDescription,
    IReadOnlyList<string> SpiceBlendNames,
    IReadOnlyList<NutritionSuggestionComponent> Components,
    IReadOnlyList<NutritionSuggestionComponentGroup> ComponentGroups,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float Score,
    string Style,
    string IconGlyph,
    string DirectionsText);

/// <summary>One meal slot of the generated day plan: its target, and either a suggestion or the reason there</summary>
/// is none.</summary>
public sealed record NutritionDayMealPlan(
    string MealType,
    float TargetCalories,
    float TargetProtein,
    float TargetCarbs,
    float TargetFats,
    float DailyCaloriesTarget,
    bool IsAlreadyLogged,
    NutritionMealSuggestion? Suggestion,
    string ErrorMessage = "");

/// <summary>An ingredient of a suggested meal, with the amount in the unit it is displayed in.</summary>
public sealed record NutritionMealIngredient(
    int FoodItemId,
    string Name,
    float Grams,
    float Amount,
    string ServingLabel,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    string SourceSummary);

/// <summary>The food the custom-food form created, shaped so the caller can log it or add it to a recipe.</summary>
public sealed record CustomFoodResult(
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
    string NutritionSummary);
