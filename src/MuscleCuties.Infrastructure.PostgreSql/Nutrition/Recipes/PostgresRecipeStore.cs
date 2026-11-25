using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Recipes;

/// <summary>Owner-scoped PostgreSQL recipe and meal template data store.</summary>
public sealed class PostgresRecipeStore(PostgresDbContext db) : IRecipeStore
{
    private const float MinimumServingsCount = 0.01f;
    private const float MinimumWeightGrams = 0.01f;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Stores a new recipe.</summary>
    public Task<RecipeDetailsDto> SaveRecipeAsync(RecipeDetailsDto recipe, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when saving recipes to PostgreSQL.");

    /// <summary>The user's recipes.</summary>
    public Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading recipes from PostgreSQL.");

    /// <summary>One recipe with its ingredients.</summary>
    public Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading recipes from PostgreSQL.");

    /// <summary>Deletes a recipe.</summary>
    public Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when deleting recipes from PostgreSQL.");

    /// <summary>Stores a new meal template.</summary>
    public Task<MealTemplateDto> SaveMealTemplateAsync(MealTemplateDto template, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when saving meal templates to PostgreSQL.");

    /// <summary>Her meal templates as summaries, newest first.</summary>
    /// <summary>Her meal templates as summaries, newest first, optionally narrowed to one meal type.</summary>
    public Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(int? mealType = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading meal templates from PostgreSQL.");

    /// <summary>One meal template by id.</summary>
    public Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when reading meal templates from PostgreSQL.");

    /// <summary>Deletes a meal template.</summary>
    public Task DeleteMealTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A tenant user ID is required when deleting meal templates from PostgreSQL.");

    // Tenant-authoritative PostgreSQL operations:

    /// <summary>Stores a recipe as a validated JSON snapshot, so a recipe can gain fields without a migration.</summary>
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

    /// <summary>Updates a recipe and its ingredient rows.</summary>
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

    /// <summary>Her recipes as summaries, newest first, without reading the whole snapshot of each.</summary>
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

    /// <summary>One recipe in full, or nothing when it is not hers.</summary>
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

    /// <summary>Deletes one of her recipes; the account predicate is part of the delete, so another account's row cannot match.</summary>
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

    /// <summary>Stores a meal template as a validated JSON snapshot.</summary>
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

    /// <summary>Her meal templates as summaries, newest first, optionally narrowed to one meal type.</summary>
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

    /// <summary>One template in full, or nothing when it is not hers.</summary>
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

    /// <summary>Deletes one of her templates, scoped to her account in the predicate itself.</summary>
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

    /// <summary>Runs the action with the tenant setting applied.</summary>
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

    /// <summary>Refuses a recipe with no name or no lines before it is stored, so a stored snapshot is always renderable.</summary>
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

    /// <summary>The same check for a meal template.</summary>
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

    /// <summary>The summary shape of a recipe.</summary>
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
