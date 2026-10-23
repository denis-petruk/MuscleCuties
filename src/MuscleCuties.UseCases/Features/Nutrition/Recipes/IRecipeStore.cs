using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Recipes;

public interface IRecipeStore
{
    Task<RecipeDetailsDto> SaveRecipeAsync(RecipeDetailsDto recipe, CancellationToken cancellationToken = default);
    Task<RecipeDetailsDto> UpdateRecipeAsync(RecipeDetailsDto recipe, int expectedVersion, CancellationToken cancellationToken = default) =>
        SaveRecipeAsync(recipe with { ConcurrencyVersion = expectedVersion + 1 }, cancellationToken);
    Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(CancellationToken cancellationToken = default);
    Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MealTemplateDto> SaveMealTemplateAsync(MealTemplateDto template, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(int? mealType = null, CancellationToken cancellationToken = default);
    Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteMealTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RecipeDetailsDto> SaveRecipeAsync(Guid userId, RecipeDetailsDto recipe, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped recipe writes require an explicit store implementation.");
    Task<RecipeDetailsDto> UpdateRecipeAsync(Guid userId, RecipeDetailsDto recipe, int expectedVersion, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped recipe updates require an explicit store implementation.");
    Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped recipe reads require an explicit store implementation.");
    Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped recipe reads require an explicit store implementation.");
    Task DeleteRecipeAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped recipe deletes require an explicit store implementation.");

    Task<MealTemplateDto> SaveMealTemplateAsync(Guid userId, MealTemplateDto template, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped template writes require an explicit store implementation.");
    Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(Guid userId, int? mealType = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped template reads require an explicit store implementation.");
    Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped template reads require an explicit store implementation.");
    Task DeleteMealTemplateAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tenant-scoped template deletes require an explicit store implementation.");
}
