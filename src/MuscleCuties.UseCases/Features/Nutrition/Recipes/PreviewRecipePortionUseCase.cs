using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Recipes;

public interface IPreviewRecipePortionUseCase
{
    Task<RecipePortionPreviewDto> ExecuteAsync(Guid userId, PreviewRecipePortionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PreviewRecipePortionUseCase(IRecipeStore recipeStore) : IPreviewRecipePortionUseCase
{
    public async Task<RecipePortionPreviewDto> ExecuteAsync(Guid userId, PreviewRecipePortionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty)
            throw new ArgumentException("A user ID is required.", nameof(userId));
        if (request.RecipeId == Guid.Empty)
            throw new ArgumentException("A recipe ID is required.", nameof(request));

        var recipe = await recipeStore.GetRecipeByIdAsync(userId, request.RecipeId, cancellationToken)
            .ConfigureAwait(false);
        if (recipe is null)
            throw new KeyNotFoundException("Recipe was not found for this user.");

        var portion = RecipeBatchCalculationRule.CalculatePortion(new RecipeBatchTotals(
            recipe.ServingsCount, recipe.TotalRawWeightGrams, recipe.CookedWeightGrams,
            recipe.TotalCalories, recipe.TotalProtein, recipe.TotalCarbs,
            recipe.TotalNetCarbs, recipe.TotalFats), request.ServingsCount, request.Grams);

        return new RecipePortionPreviewDto(recipe.Id, recipe.Name,
            portion.Batch.ServingsCount, portion.Batch.TotalRawWeightGrams,
            portion.Batch.CookedWeightGrams, portion.Batch.TotalCalories,
            portion.Batch.TotalProtein, portion.Batch.TotalCarbs, portion.Batch.TotalNetCarbs,
            portion.Batch.TotalFats, portion.SelectedServingsCount, portion.SelectedGrams,
            portion.Calories, portion.Protein, portion.Carbs, portion.NetCarbs, portion.Fats);
    }
}
