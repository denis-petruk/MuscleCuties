namespace MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

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

public sealed record CreateRecipeRequest(
    string Name,
    string? Description,
    float ServingsCount,
    float? CookedWeightGrams,
    IReadOnlyList<RecipeIngredientInputDto> Ingredients
);

public sealed record UpdateRecipeRequest(
    Guid RecipeId,
    int ExpectedVersion,
    string Name,
    string? Description,
    float ServingsCount,
    float? CookedWeightGrams,
    IReadOnlyList<RecipeIngredientInputDto> Ingredients
);

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

public sealed record PreviewRecipePortionRequest(
    Guid RecipeId,
    float? ServingsCount,
    float? Grams);

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

public sealed record FoodMassConversionOutputDto(bool Succeeded, float? Grams);
public sealed record FoodServingOptionOutputDto(string Label, string Unit, float Grams);
public sealed record FoodPortionCatalogOutputDto(
    string GramUnit,
    IReadOnlyList<string> SupportedUnits,
    IReadOnlyList<FoodServingOptionOutputDto> StandardOptions);

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

public sealed record SaveMealAsTemplateRequest(
    string TemplateName,
    int MealType,
    IReadOnlyList<MealTemplateItemDto> Items
);

public sealed record RecipesAndTemplatesBundleDto(
    IReadOnlyList<RecipeSummaryDto> Recipes,
    IReadOnlyList<MealTemplateDto> Templates
);
