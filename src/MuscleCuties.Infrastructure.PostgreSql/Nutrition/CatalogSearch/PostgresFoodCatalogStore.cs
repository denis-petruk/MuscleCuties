using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.CatalogSearch;

/// <summary>Read-only adapters for the globally shared canonical nutrition catalog.</summary>
public sealed class PostgresFoodCatalogStore(PostgresDbContext dbContext)
    : IFoodCatalogSearchStore, IBarcodeFoodLookupStore
{
    private const int MinimumSearchTokenLength = 2;

    public async Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        await SearchCandidatesAsync(query, null, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var tokens = FoodCatalogRankingRule.Tokenize(query)
            .Where(token => token.Length >= MinimumSearchTokenLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tokens.Length == 0)
            return [];

        IQueryable<Models.Catalog.FoodItem> foods = dbContext.CatalogFoodItems.AsNoTracking();
        foreach (var token in tokens)
        {
            var pattern = $"%{EscapeLike(FoodCatalogRankingRule.SearchPrefilterStem(token))}%";
            foods = foods.Where(food =>
                EF.Functions.ILike(food.Name, pattern, "\\") ||
                (food.BrandName != null && EF.Functions.ILike(food.BrandName, pattern, "\\")) ||
                (food.BrandOwner != null && EF.Functions.ILike(food.BrandOwner, pattern, "\\")) ||
                (food.GtinUpc != null && EF.Functions.ILike(food.GtinUpc, pattern, "\\")) ||
                (food.Ingredients != null && EF.Functions.ILike(food.Ingredients, pattern, "\\")));
        }

        var records = await foods
            .OrderBy(food => food.Name)
            .ThenBy(food => food.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidates = records.Select(ToCandidate).ToList();
        if (ownerUserId is not null)
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var customRecords = await strategy.ExecuteAsync<List<Models.App.UserFoodItemRecord>>(async strategyCancellationToken =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(strategyCancellationToken)
                    .ConfigureAwait(false);
                await PostgresTenantContext.SetForCurrentTransactionAsync(
                    dbContext, ownerUserId.Value, strategyCancellationToken).ConfigureAwait(false);
                IQueryable<Models.App.UserFoodItemRecord> customFoods = dbContext.UserFoodItems.AsNoTracking()
                    .Where(food => food.UserId == ownerUserId.Value);
                foreach (var token in tokens)
                {
                    var pattern = $"%{EscapeLike(FoodCatalogRankingRule.SearchPrefilterStem(token))}%";
                    customFoods = customFoods.Where(food =>
                        EF.Functions.ILike(food.Name, pattern, "\\") ||
                        (food.BrandName != null && EF.Functions.ILike(food.BrandName, pattern, "\\")) ||
                        (food.BrandOwner != null && EF.Functions.ILike(food.BrandOwner, pattern, "\\")) ||
                        (food.GtinUpc != null && EF.Functions.ILike(food.GtinUpc, pattern, "\\")) ||
                        (food.Ingredients != null && EF.Functions.ILike(food.Ingredients, pattern, "\\")));
                }
                var found = await customFoods
                    .OrderBy(food => food.Name)
                    .ThenBy(food => food.Id)
                    .ToListAsync(strategyCancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(strategyCancellationToken).ConfigureAwait(false);
                return found;
            }, cancellationToken).ConfigureAwait(false);
            candidates.AddRange(customRecords.Select(ToCandidate));
        }
        return candidates;
    }

    public async Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
        IReadOnlyList<string> exactValues,
        CancellationToken cancellationToken = default) =>
        await FindFoodByBarcodeAsync(exactValues, null, cancellationToken).ConfigureAwait(false);

    public async Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
        IReadOnlyList<string> exactValues,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exactValues);
        var values = exactValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0)
            return null;

        var matches = await dbContext.CatalogFoodItems.AsNoTracking()
            .Where(food => food.GtinUpc != null && values.Contains(food.GtinUpc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var canonicalMatch = matches
            .OrderBy(food => Array.IndexOf(values, food.GtinUpc!))
            .ThenBy(food => food.Id)
            .FirstOrDefault();
        if (ownerUserId is not null)
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var customMatch = await strategy.ExecuteAsync<List<Models.App.UserFoodItemRecord>>(async strategyCancellationToken =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(strategyCancellationToken)
                    .ConfigureAwait(false);
                await PostgresTenantContext.SetForCurrentTransactionAsync(
                    dbContext, ownerUserId.Value, strategyCancellationToken).ConfigureAwait(false);
                var found = await dbContext.UserFoodItems.AsNoTracking()
                    .Where(food => food.UserId == ownerUserId.Value && food.GtinUpc != null && values.Contains(food.GtinUpc))
                    .ToListAsync(strategyCancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(strategyCancellationToken).ConfigureAwait(false);
                return found;
            }, cancellationToken).ConfigureAwait(false);
            var preferredCustom = customMatch
                .OrderBy(food => Array.IndexOf(values, food.GtinUpc!))
                .ThenBy(food => food.Id)
                .FirstOrDefault();
            if (preferredCustom is not null)
                return ToCandidate(preferredCustom);
        }
        return canonicalMatch is null ? null : ToCandidate(canonicalMatch);
    }

    private static FoodCatalogCandidate ToCandidate(Models.Catalog.FoodItem food) => new(
        food.Id,
        FoodCatalogRankingRule.FormatFoodName(food.Name),
        food.Calories is null ? null : (float)food.Calories.Value,
        food.Protein is null ? null : (float)food.Protein.Value,
        food.Carbs is null ? null : (float)food.Carbs.Value,
        food.Fats is null ? null : (float)food.Fats.Value,
        false,
        food.FdcId,
        food.DataType,
        food.BrandName,
        food.BrandOwner,
        food.GtinUpc,
        food.Ingredients,
        food.ServingSize is null ? null : (float)food.ServingSize.Value,
        food.ServingSizeUnit,
        food.ServingOptionsJson);

    private static FoodCatalogCandidate ToCandidate(Models.App.UserFoodItemRecord food) => new(
        LegacyCustomNumericId(food.Id),
        FoodCatalogRankingRule.FormatFoodName(food.Name),
        food.Calories is null ? null : (float)food.Calories.Value,
        food.Protein is null ? null : (float)food.Protein.Value,
        food.Carbs is null ? null : (float)food.Carbs.Value,
        food.Fats is null ? null : (float)food.Fats.Value,
        true,
        food.FdcId,
        food.DataType,
        food.BrandName,
        food.BrandOwner,
        food.GtinUpc,
        food.Ingredients,
        food.ServingSize is null ? null : (float)food.ServingSize.Value,
        food.ServingSizeUnit,
        food.ServingOptionsJson,
        food.Id);

    private static int LegacyCustomNumericId(Guid id)
    {
        var bytes = id.ToByteArray();
        return BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, sizeof(int));
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
