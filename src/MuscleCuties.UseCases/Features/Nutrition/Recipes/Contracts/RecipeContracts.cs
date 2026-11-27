namespace MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

/// <summary>An ingredient she is adding to a recipe, with its weight.</summary>
public sealed record RecipeIngredientInputDto(
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
    string? SourceVersion = null
);

/// <summary>An ingredient of a stored recipe.</summary>
public sealed record RecipeIngredientDto(
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
    string? SourceVersion = null
);

/// <summary>A recipe to create from its ingredients.</summary>
public sealed record CreateRecipeRequest(
    string Name,
    string? Description,
    float ServingsCount,
    float? CookedWeightGrams,
    IReadOnlyList<RecipeIngredientInputDto> Ingredients
);

/// <summary>A change to a stored recipe.</summary>
public sealed record UpdateRecipeRequest(
    Guid RecipeId,
    int ExpectedVersion,
    string Name,
    string? Description,
    float ServingsCount,
    float? CookedWeightGrams,
    IReadOnlyList<RecipeIngredientInputDto> Ingredients
);

/// <summary>A recipe as the list shows it.</summary>
public sealed record RecipeSummaryDto(
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
    int ConcurrencyVersion = 1
);

/// <summary>A recipe in full, with its ingredients and totals.</summary>
public sealed record RecipeDetailsDto(
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
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    DateTime CreatedAtUtc,
    int ConcurrencyVersion = 1
);

/// <summary>A request to convert a portion into grams.</summary>
public sealed record CalculateRecipePortionRequest(
    Guid RecipeId,
    string RecipeName,
    float ServingsCount,
    float PerServingGrams,
    float PerServingCalories,
    float PerServingProtein,
    float PerServingCarbs,
    float PerServingNetCarbs,
    float PerServingFats,
    float? RequestedServingsCount,
    float? RequestedGrams);

/// <summary>Whether the conversion succeeded, and the grams when it did.</summary>
public sealed record FoodMassConversionOutputDto(bool Succeeded, float? Grams);
/// <summary>One way to measure a food, with its weight in grams.</summary>
public sealed record FoodServingOptionOutputDto(string Label, string Unit, float Grams);
/// <summary>The measures available for a food.</summary>
public sealed record FoodPortionCatalogOutputDto(
    string GramUnit,
    IReadOnlyList<string> SupportedUnits,
    IReadOnlyList<FoodServingOptionOutputDto> StandardOptions);

/// <summary>What a portion of a recipe would contribute.</summary>
public sealed record RecipePortionPreviewDto(
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

/// <summary>One food inside a meal template.</summary>
public sealed record MealTemplateItemDto(
    Guid Id,
    string Name,
    float ServingSize,
    string ServingUnit,
    float ServingsCount,
    float Calories,
    float Protein,
    float Carbs,
    float Fats
);

/// <summary>A meal template with its foods.</summary>
public sealed record MealTemplateDto(
    Guid Id,
    string Name,
    int MealType,
    float TotalCalories,
    float TotalProtein,
    float TotalCarbs,
    float TotalFats,
    int ItemCount,
    IReadOnlyList<MealTemplateItemDto> Items,
    DateTime CreatedAtUtc
);

/// <summary>A logged meal saved as a reusable template.</summary>
public sealed record SaveMealAsTemplateRequest(
    string TemplateName,
    int MealType,
    IReadOnlyList<MealTemplateItemDto> Items
);

/// <summary>Her recipes and templates in one payload, which is what the screen needs.</summary>
public sealed record RecipesAndTemplatesBundleDto(
    IReadOnlyList<RecipeSummaryDto> Recipes,
    IReadOnlyList<MealTemplateDto> Templates
);
