using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Recipes;

/// <summary>Owner-scoped PostgreSQL recipe and meal template data store.</summary>
public sealed class PostgresRecipeStore(PostgresDbContext db) : IRecipeStore
{
    private const float MinimumServingsCount = 0.01f;
    private const float MinimumWeightGrams = 0.01f;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<RecipeDetailsDto> SaveRecipeAsync(RecipeDetailsDto recipe, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when saving recipes to PostgreSQL.");

    public Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading recipes from PostgreSQL.");

    public Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading recipes from PostgreSQL.");

    public Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when deleting recipes from PostgreSQL.");

    public Task<MealTemplateDto> SaveMealTemplateAsync(MealTemplateDto template, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when saving meal templates to PostgreSQL.");

    public Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(int? mealType = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading meal templates from PostgreSQL.");

    public Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading meal templates from PostgreSQL.");

    public Task DeleteMealTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when deleting meal templates from PostgreSQL.");

    // Tenant-authoritative PostgreSQL operations:

    public async Task<RecipeDetailsDto> SaveRecipeAsync(Guid userId, RecipeDetailsDto recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ValidateRecipe(recipe);

        return await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var existing = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == recipe.Id && r.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var existingVersion = existing is null
                ? 0
                : (JsonSerializer.Deserialize<RecipeDetailsDto>(existing.SnapshotJson, JsonOptions)?.ConcurrencyVersion ?? 1);
            var nextVersion = Math.Max(recipe.ConcurrencyVersion, existingVersion + 1);
            var persisted = recipe with { ConcurrencyVersion = nextVersion };
            var json = JsonSerializer.Serialize(persisted, JsonOptions);
            if (existing is null)
            {
                await db.Recipes.AddAsync(new RecipeRecord
                {
                    Id = persisted.Id,
                    UserId = userId,
                    Name = persisted.Name.Trim(),
                    SnapshotJson = json,
                    CreatedAtUtc = persisted.CreatedAtUtc == default ? DateTime.UtcNow : persisted.CreatedAtUtc
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                existing.Name = persisted.Name.Trim();
                existing.SnapshotJson = json;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return persisted;
        }).ConfigureAwait(false);
    }

    public async Task<RecipeDetailsDto> UpdateRecipeAsync(
        Guid userId,
        RecipeDetailsDto recipe,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ValidateRecipe(recipe);

        return await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var existing = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == recipe.Id && r.UserId == userId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
                throw new KeyNotFoundException("Recipe was not found for this user.");

            var currentDetails = JsonSerializer.Deserialize<RecipeDetailsDto>(existing.SnapshotJson, JsonOptions);
            var currentVersion = currentDetails?.ConcurrencyVersion ?? 1;
            if (currentVersion != expectedVersion)
                throw new InvalidOperationException($"Recipe version conflict (expected {expectedVersion}, current {currentVersion}).");

            var updated = recipe with
            {
                CreatedAtUtc = currentDetails?.CreatedAtUtc ?? existing.CreatedAtUtc,
                ConcurrencyVersion = currentVersion + 1
            };
            existing.Name = updated.Name.Trim();
            existing.SnapshotJson = JsonSerializer.Serialize(updated, JsonOptions);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return updated;
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var rows = await db.Recipes.AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var results = new List<RecipeSummaryDto>(rows.Count);
            foreach (var row in rows)
            {
                var details = JsonSerializer.Deserialize<RecipeDetailsDto>(row.SnapshotJson, JsonOptions);
                if (details is not null)
                {
                    results.Add(ToSummary(details));
                }
            }

            return (IReadOnlyList<RecipeSummaryDto>)results;
        }).ConfigureAwait(false);
    }

    public async Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var row = await db.Recipes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
                return null;

            return JsonSerializer.Deserialize<RecipeDetailsDto>(row.SnapshotJson, JsonOptions);
        }).ConfigureAwait(false);
    }

    public async Task DeleteRecipeAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var row = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (row is not null)
            {
                db.Recipes.Remove(row);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return true;
        }).ConfigureAwait(false);
    }

    public async Task<MealTemplateDto> SaveMealTemplateAsync(Guid userId, MealTemplateDto template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ValidateTemplate(template);

        return await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var existing = await db.SavedMealTemplates
                .FirstOrDefaultAsync(t => t.Id == template.Id && t.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var json = JsonSerializer.Serialize(template, JsonOptions);
            var mealTypeString = template.MealType.ToString();

            if (existing is null)
            {
                await db.SavedMealTemplates.AddAsync(new SavedMealTemplateRecord
                {
                    Id = template.Id,
                    UserId = userId,
                    Name = template.Name.Trim(),
                    MealType = mealTypeString,
                    SnapshotJson = json,
                    CreatedAtUtc = template.CreatedAtUtc == default ? DateTime.UtcNow : template.CreatedAtUtc
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                existing.Name = template.Name.Trim();
                existing.MealType = mealTypeString;
                existing.SnapshotJson = json;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return template;
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(Guid userId, int? mealType = null, CancellationToken cancellationToken = default)
    {
        return await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var query = db.SavedMealTemplates.AsNoTracking().Where(t => t.UserId == userId);
            if (mealType.HasValue)
            {
                var mtStr = mealType.Value.ToString();
                query = query.Where(t => t.MealType == mtStr);
            }

            var rows = await query.OrderByDescending(t => t.CreatedAtUtc)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var results = new List<MealTemplateDto>(rows.Count);
            foreach (var row in rows)
            {
                var template = JsonSerializer.Deserialize<MealTemplateDto>(row.SnapshotJson, JsonOptions);
                if (template is not null)
                {
                    results.Add(template);
                }
            }

            return (IReadOnlyList<MealTemplateDto>)results;
        }).ConfigureAwait(false);
    }

    public async Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var row = await db.SavedMealTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
                return null;

            return JsonSerializer.Deserialize<MealTemplateDto>(row.SnapshotJson, JsonOptions);
        }).ConfigureAwait(false);
    }

    public async Task DeleteMealTemplateAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        await WithTenantAsync(userId, cancellationToken, async () =>
        {
            var row = await db.SavedMealTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (row is not null)
            {
                db.SavedMealTemplates.Remove(row);
            }

            var legacy = await db.UserMealTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (legacy is not null)
            {
                var legacyEntries = await db.UserMealTemplateEntries
                    .Where(e => e.MealTemplateId == id && e.UserId == userId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                db.UserMealTemplateEntries.RemoveRange(legacyEntries);
                db.UserMealTemplates.Remove(legacy);
            }

            if (row is not null || legacy is not null)
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return true;
        }).ConfigureAwait(false);
    }

    private async Task<T> WithTenantAsync<T>(Guid userId, CancellationToken ct, Func<Task<T>> action) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, ct).ConfigureAwait(false);
            var result = await action().ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);

    private static void ValidateRecipe(RecipeDetailsDto recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.Name))
            throw new ArgumentException("Recipe name cannot be empty.", nameof(recipe));

        if (!float.IsFinite(recipe.ServingsCount) || recipe.ServingsCount < MinimumServingsCount)
            throw new ArgumentOutOfRangeException(nameof(recipe), "Servings count must be positive and finite.");

        if (recipe.CookedWeightGrams.HasValue &&
            (!float.IsFinite(recipe.CookedWeightGrams.Value) || recipe.CookedWeightGrams.Value < MinimumWeightGrams))
            throw new ArgumentOutOfRangeException(nameof(recipe), "Cooked weight must be positive and finite.");

        if (!float.IsFinite(recipe.TotalCalories) || recipe.TotalCalories < 0f ||
            !float.IsFinite(recipe.TotalProtein) || recipe.TotalProtein < 0f ||
            !float.IsFinite(recipe.TotalCarbs) || recipe.TotalCarbs < 0f ||
            !float.IsFinite(recipe.TotalFats) || recipe.TotalFats < 0f)
            throw new ArgumentOutOfRangeException(nameof(recipe), "Recipe macro totals must be non-negative and finite.");
    }

    private static void ValidateTemplate(MealTemplateDto template)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
            throw new ArgumentException("Template name cannot be empty.", nameof(template));

        if (!float.IsFinite(template.TotalCalories) || template.TotalCalories < 0f ||
            !float.IsFinite(template.TotalProtein) || template.TotalProtein < 0f ||
            !float.IsFinite(template.TotalCarbs) || template.TotalCarbs < 0f ||
            !float.IsFinite(template.TotalFats) || template.TotalFats < 0f)
            throw new ArgumentOutOfRangeException(nameof(template), "Template macro totals must be non-negative and finite.");
    }

    private static RecipeSummaryDto ToSummary(RecipeDetailsDto r) => new(
        Id: r.Id,
        Name: r.Name,
        ServingsCount: r.ServingsCount,
        PerServingCalories: r.PerServingCalories,
        PerServingProtein: r.PerServingProtein,
        PerServingCarbs: r.PerServingCarbs,
        PerServingNetCarbs: r.PerServingNetCarbs,
        PerServingFats: r.PerServingFats,
        PerServingGrams: r.PerServingGrams,
        IngredientCount: r.Ingredients.Count,
        CreatedAtUtc: r.CreatedAtUtc);
}
