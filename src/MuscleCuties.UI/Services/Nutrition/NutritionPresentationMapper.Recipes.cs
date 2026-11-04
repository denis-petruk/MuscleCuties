using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

public static partial class NutritionPresentationMapper
{
    private const float TemplateGramsPerHundred = 100f;
    private const float FallbackTemplateGrams = 100f;
    private const string TemplateGramUnit = "g";
    private const string TemplateSourceLabel = "Template";

    public static MealIngredientInputDto ToTemplateMealIngredient(MealTemplateItem item)
    {
        var unit = string.IsNullOrWhiteSpace(item.ServingUnit) ? TemplateGramUnit : item.ServingUnit;
        var isMassUnit = string.Equals(unit, "g", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(unit, "ml", StringComparison.OrdinalIgnoreCase);
        var grams = isMassUnit
            ? Math.Max(1f, item.ServingSize * item.ServingsCount)
            : FallbackTemplateGrams;
        var factor = TemplateGramsPerHundred / grams;
        return new MealIngredientInputDto(
            0,
            item.Name,
            grams,
            grams,
            unit,
            item.Calories * factor,
            item.Protein * factor,
            item.Carbs * factor,
            item.Fats * factor,
            TemplateSourceLabel);
    }

    public static MealTemplate FromMealTemplate(MealTemplateDto item) =>
        new(item.Id, item.Name, item.MealType, item.TotalCalories, item.TotalProtein, item.TotalCarbs,
            item.TotalFats, item.ItemCount, item.Items.Select(FromMealTemplateItem).ToList(), item.CreatedAtUtc);

    public static MealTemplateItem FromMealTemplateItem(MealTemplateItemDto item) =>
        new(item.Id, item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories,
            item.Protein, item.Carbs, item.Fats);

    public static MealTemplateDto ToMealTemplate(MealTemplate item) =>
        new(item.Id, item.Name, item.MealType, item.TotalCalories, item.TotalProtein, item.TotalCarbs,
            item.TotalFats, item.ItemCount, item.Items.Select(ToMealTemplateItem).ToList(), item.CreatedAtUtc);

    public static MealTemplateItemDto ToMealTemplateItem(MealTemplateItem item) =>
        new(item.Id, item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories,
            item.Protein, item.Carbs, item.Fats);

    public static RecipeSummaryItem FromRecipeSummary(RecipeSummaryDto item) =>
        new(item.Id, item.Name, item.ServingsCount, item.PerServingCalories, item.PerServingProtein,
            item.PerServingCarbs, item.PerServingNetCarbs, item.PerServingFats, item.PerServingGrams,
            item.IngredientCount, item.CreatedAtUtc, item.ConcurrencyVersion);

    public static RecipeDetailsItem FromRecipeDetails(RecipeDetailsDto item) =>
        new(item.Id, item.Name, item.Description, item.ServingsCount, item.CookedWeightGrams,
            item.TotalRawWeightGrams, item.TotalCalories, item.TotalProtein, item.TotalCarbs,
            item.TotalNetCarbs, item.TotalFats, item.PerServingCalories, item.PerServingProtein,
            item.PerServingCarbs, item.PerServingNetCarbs, item.PerServingFats, item.PerServingGrams,
            item.Ingredients.Select(i => new RecipeIngredient(i.Id, i.FoodItemId, i.Name, i.Grams, i.Calories,
                i.Protein, i.Carbs, i.Fats, i.Fiber, i.SugarAlcohols, i.NetCarbs, i.SourceKind, i.SourceLabel,
                i.SourceVersion)).ToList(), item.CreatedAtUtc, item.ConcurrencyVersion);

    public static RecipePortionPreview FromRecipePortion(RecipePortionPreviewDto item) =>
        new(item.RecipeId, item.RecipeName, item.BatchServingsCount, item.BatchRawWeightGrams,
            item.BatchCookedWeightGrams, item.BatchCalories, item.BatchProtein, item.BatchCarbs,
            item.BatchNetCarbs, item.BatchFats, item.SelectedServingsCount, item.SelectedGrams,
            item.Calories, item.Protein, item.Carbs, item.NetCarbs, item.Fats);

    public static RecipePortionPreview CalculateRecipePortion(
        IRecipePortionUseCase useCase,
        RecipeSummaryItem recipe,
        float? servings,
        float? grams) =>
        FromRecipePortion(useCase.Calculate(new CalculateRecipePortionRequest(
            recipe.Id, recipe.Name, recipe.ServingsCount, recipe.PerServingGrams,
            recipe.PerServingCalories, recipe.PerServingProtein, recipe.PerServingCarbs,
            recipe.PerServingNetCarbs, recipe.PerServingFats, servings, grams)));

    public static SaveMealAsTemplateRequest CreateSaveMealTemplateRequest(
        string name,
        int mealType,
        IEnumerable<MealTemplateItem> items) =>
        new(name, mealType, items.Select(ToMealTemplateItem).ToList());

    public static Task<RecipeDetailsItem> PreviewRecipeAsync(
        ICreateRecipeUseCase useCase,
        string name,
        string? description,
        float servings,
        float? cookedWeight,
        IEnumerable<RecipeIngredientInput> ingredients) =>
        PreviewCoreAsync(useCase, new CreateRecipeRequest(name, description, servings, cookedWeight,
            ingredients.Select(ToRecipeIngredientInput).ToList()));

    private static async Task<RecipeDetailsItem> PreviewCoreAsync(
        ICreateRecipeUseCase useCase,
        CreateRecipeRequest request) =>
        FromRecipeDetails(await useCase.PreviewAsync(request));

    public static async Task<RecipeDetailsItem> SaveRecipeAsync(
        ICreateRecipeUseCase useCase,
        Guid? recipeId,
        int version,
        string name,
        string? description,
        float servings,
        float? cookedWeight,
        IEnumerable<RecipeIngredientInput> ingredients)
    {
        var request = new CreateRecipeRequest(name, description, servings, cookedWeight,
            ingredients.Select(ToRecipeIngredientInput).ToList());
        var result = recipeId is { } id && id != Guid.Empty
            ? await useCase.UpdateAsync(new UpdateRecipeRequest(id, version, name, description, servings,
                cookedWeight, request.Ingredients))
            : await useCase.ExecuteAsync(request);
        return FromRecipeDetails(result);
    }

    private static RecipeIngredientInputDto ToRecipeIngredientInput(RecipeIngredientInput item) =>
        new(item.FoodItemId, item.Name, item.Grams, item.Calories, item.Protein, item.Carbs, item.Fats,
            item.Fiber, item.SugarAlcohols, item.SourceKind, item.SourceLabel, item.SourceVersion);
}
