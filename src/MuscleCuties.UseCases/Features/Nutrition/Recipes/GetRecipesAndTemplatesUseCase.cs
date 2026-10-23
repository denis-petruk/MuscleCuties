using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Recipes;

public interface IGetRecipesAndTemplatesUseCase
{
    Task<RecipesAndTemplatesBundleDto> ExecuteAsync(int? mealType = null, CancellationToken cancellationToken = default);
    Task<RecipesAndTemplatesBundleDto> ExecuteAsync(Guid userId, int? mealType = null, CancellationToken cancellationToken = default);
}

public sealed class GetRecipesAndTemplatesUseCase : IGetRecipesAndTemplatesUseCase
{
    private readonly IRecipeStore _recipeStore;

    public GetRecipesAndTemplatesUseCase(IRecipeStore recipeStore)
    {
        _recipeStore = recipeStore ?? throw new ArgumentNullException(nameof(recipeStore));
    }

    public async Task<RecipesAndTemplatesBundleDto> ExecuteAsync(int? mealType = null, CancellationToken cancellationToken = default)
    {
        var recipesTask = _recipeStore.GetRecipesAsync(cancellationToken);
        var templatesTask = _recipeStore.GetMealTemplatesAsync(mealType, cancellationToken);

        await Task.WhenAll(recipesTask, templatesTask);

        return new RecipesAndTemplatesBundleDto(
            Recipes: await recipesTask,
            Templates: await templatesTask
        );
    }

    public async Task<RecipesAndTemplatesBundleDto> ExecuteAsync(Guid userId, int? mealType = null, CancellationToken cancellationToken = default)
    {
        var recipes = await _recipeStore.GetRecipesAsync(userId, cancellationToken).ConfigureAwait(false);
        var templates = await _recipeStore.GetMealTemplatesAsync(userId, mealType, cancellationToken).ConfigureAwait(false);

        return new RecipesAndTemplatesBundleDto(
            Recipes: recipes,
            Templates: templates
        );
    }
}
