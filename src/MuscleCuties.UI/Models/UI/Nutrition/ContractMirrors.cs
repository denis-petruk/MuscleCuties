using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;

namespace MuscleCuties.UI.Models.UI.Nutrition;

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
    public IReadOnlyList<FoodHistoryIngredient> Ingredients { get; init; } = [];
    public string? AcceptedSuggestionName { get; init; }
    public CyclePhase? AcceptedSuggestionPhase { get; init; }
    public int? LoggedMealId { get; init; }
    public bool IsGroupedMeal => Ingredients.Count > 0;
}

public sealed record FoodHistoryIngredient(int FoodItemId, string Name, float Grams);
public sealed record LoggedFoodIngredient(int FoodItemId, string Name, float Grams);

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
    public int? LoggedMealId { get; init; }
    public bool IsGroupedMeal => Ingredients.Count > 0;
}

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
    public Guid? SourceId { get; init; }
    public int? IngredientFoodItemId { get; init; }
    public IReadOnlyList<FoodHistoryIngredient> Ingredients { get; init; } = [];
}

public sealed record WaterQuickAddDefaults(IReadOnlyList<int> VolumesMillilitres);
public sealed record FastLookupFood(string Name, double Calories, double Protein, double Carbs, double Fats, bool IsCustom);

public enum BarcodeSymbology
{
    Auto = 0,
    Ean13 = 1,
    UpcA = 2,
    Ean8 = 3,
    UpcE = 4,
    Code128 = 5
}

public enum BarcodeCaptureOutcome
{
    Decoded = 0,
    Cancelled = 1,
    PermissionDenied = 2,
    CameraUnavailable = 3,
    UnsupportedSymbology = 4
}

public sealed record BarcodeCameraCaptureResult(
    BarcodeCaptureOutcome Outcome,
    string? BarcodeText = null,
    BarcodeSymbology Symbology = BarcodeSymbology.Auto,
    string? ErrorMessage = null);

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

public sealed record FoodServingOption(string Label, string Unit, float Grams);

public sealed record FoodPortionCatalog(
    string GramUnit,
    IReadOnlyList<string> SupportedUnits,
    IReadOnlyList<FoodServingOption> StandardOptions);

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

public sealed record RecipePortionPreview(
    Guid RecipeId,
    string RecipeName,
    float BatchServingsCount,
    float BatchRawWeightGrams,
    float? BatchCookedWeightGrams,
    float BatchCalories,
    float BatchProtein,
    float BatchCarbs,
    float BatchNetCarbs,
    float BatchFats,
    float SelectedServingsCount,
    float SelectedGrams,
    float Calories,
    float Protein,
    float Carbs,
    float NetCarbs,
    float Fats);

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

public sealed record NutritionMealDistribution(float Breakfast, float Lunch, float Dinner, float Snack);

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

public sealed record DailyMicronutrient(
    string Group,
    string Name,
    string Unit,
    float Amount,
    float Goal);

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

public sealed record NutritionSuggestionComponentGroup(
    string ComponentType,
    string Title,
    IReadOnlyList<NutritionSuggestionComponent> Items);

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

public enum NutritionCyclePhase
{
    Unknown,
    Menstrual,
    Follicular,
    Ovulation,
    Luteal
}
