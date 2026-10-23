using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Recipes;

public interface ICreateRecipeUseCase
{
    Task<RecipeDetailsDto> PreviewAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default);
    Task<RecipeDetailsDto> ExecuteAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default);
    Task<RecipeDetailsDto> ExecuteAsync(Guid userId, CreateRecipeRequest request, CancellationToken cancellationToken = default);
    Task<RecipeDetailsDto> UpdateAsync(UpdateRecipeRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Recipe update requires an explicit use case implementation.");
    Task<RecipeDetailsDto> UpdateAsync(Guid userId, UpdateRecipeRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped recipe update requires an explicit use case implementation.");
}

public sealed class CreateRecipeUseCase : ICreateRecipeUseCase
{
    private const int InitialConcurrencyVersion = 1;
    private readonly IRecipeStore _recipeStore;

    public CreateRecipeUseCase(IRecipeStore recipeStore)
    {
        _recipeStore = recipeStore ?? throw new ArgumentNullException(nameof(recipeStore));
    }

    public async Task<RecipeDetailsDto> ExecuteAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _recipeStore.SaveRecipeAsync(BuildRecipe(request), cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecipeDetailsDto> ExecuteAsync(Guid userId, CreateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty)
            throw new ArgumentException("A user ID is required.", nameof(userId));
        return await _recipeStore.SaveRecipeAsync(userId, BuildRecipe(request), cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecipeDetailsDto> UpdateAsync(UpdateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RecipeId == Guid.Empty)
            throw new ArgumentException("A recipe ID is required.", nameof(request));
        if (request.ExpectedVersion < InitialConcurrencyVersion)
            throw new ArgumentOutOfRangeException(nameof(request), "Expected version must be positive.");

        var updated = BuildRecipe(
            new CreateRecipeRequest(request.Name, request.Description, request.ServingsCount, request.CookedWeightGrams, request.Ingredients),
            request.RecipeId,
            request.ExpectedVersion + 1);
        return await _recipeStore.UpdateRecipeAsync(updated, request.ExpectedVersion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecipeDetailsDto> UpdateAsync(Guid userId, UpdateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty)
            throw new ArgumentException("A user ID is required.", nameof(userId));
        if (request.RecipeId == Guid.Empty)
            throw new ArgumentException("A recipe ID is required.", nameof(request));
        if (request.ExpectedVersion < InitialConcurrencyVersion)
            throw new ArgumentOutOfRangeException(nameof(request), "Expected version must be positive.");

        var updated = BuildRecipe(
            new CreateRecipeRequest(request.Name, request.Description, request.ServingsCount, request.CookedWeightGrams, request.Ingredients),
            request.RecipeId,
            request.ExpectedVersion + 1);
        return await _recipeStore.UpdateRecipeAsync(userId, updated, request.ExpectedVersion, cancellationToken).ConfigureAwait(false);
    }

    public Task<RecipeDetailsDto> PreviewAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildRecipe(request));
    }

    private static RecipeDetailsDto BuildRecipe(
        CreateRecipeRequest request,
        Guid? existingId = null,
        int concurrencyVersion = InitialConcurrencyVersion)
    {
        ArgumentNullException.ThrowIfNull(request);

        var domainIngredients = (request.Ingredients ?? [])
            .Select(i => i is null ? null! : new RecipeIngredient(
                Id: Guid.NewGuid(),
                Name: i.Name,
                Grams: i.Grams,
                Calories: i.Calories,
                Protein: i.Protein,
                Carbs: i.Carbs,
                Fats: i.Fats,
                Fiber: i.Fiber,
                SugarAlcohols: i.SugarAlcohols,
                FoodItemId: i.FoodItemId,
                SourceKind: i.SourceKind ?? (i.FoodItemId.HasValue ? "Catalog" : "Manual"),
                SourceLabel: i.SourceLabel,
                SourceVersion: i.SourceVersion
            ))
            .ToList();

        var draft = RecipeDraft.Create(request.Name, request.Description, domainIngredients,
            request.ServingsCount, request.CookedWeightGrams);
        var summary = draft.Nutrition;

        var recipeId = existingId ?? Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;

        var resultIngredients = draft.Ingredients.Select(d => new RecipeIngredientDto(
            Id: d.Id,
            FoodItemId: d.FoodItemId,
            Name: d.Name,
            Grams: d.SafeGrams,
            Calories: d.SafeCalories,
            Protein: d.SafeProtein,
            Carbs: d.SafeCarbs,
            Fats: d.SafeFats,
            Fiber: d.SafeFiber,
            SugarAlcohols: d.SafeSugarAlcohols,
            NetCarbs: d.SafeNetCarbs,
            SourceKind: d.SourceKind,
            SourceLabel: d.SourceLabel,
            SourceVersion: d.SourceVersion
        )).ToList();

        return new RecipeDetailsDto(
            Id: recipeId,
            Name: draft.Name,
            Description: draft.Description,
            ServingsCount: summary.ServingsCount,
            CookedWeightGrams: draft.CookedWeightGrams,
            TotalRawWeightGrams: summary.TotalRawWeightGrams,
            TotalCalories: summary.TotalCalories,
            TotalProtein: summary.TotalProtein,
            TotalCarbs: summary.TotalCarbs,
            TotalNetCarbs: summary.TotalNetCarbs,
            TotalFats: summary.TotalFats,
            PerServingCalories: summary.PerServingCalories,
            PerServingProtein: summary.PerServingProtein,
            PerServingCarbs: summary.PerServingCarbs,
            PerServingNetCarbs: summary.PerServingNetCarbs,
            PerServingFats: summary.PerServingFats,
            PerServingGrams: summary.PerServingGrams,
            Ingredients: resultIngredients,
            CreatedAtUtc: nowUtc,
            ConcurrencyVersion: concurrencyVersion
        );
    }
}
