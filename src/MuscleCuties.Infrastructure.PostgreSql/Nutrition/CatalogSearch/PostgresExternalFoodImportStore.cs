using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.CatalogSearch;

/// <summary>
///     Stores external generic foods as owner-scoped food rows. The shared catalog stays read-only for the
///     runtime role, so an imported food is visible only to the account whose search fetched it.
/// </summary>
public sealed class PostgresExternalFoodImportStore(PostgresDbContext dbContext) : IExternalFoodImportStore
{
    private const decimal ReferenceGrams = 100m;
    private const string ReferenceUnit = "g";

    public async Task<IReadOnlyList<FoodCatalogCandidate>> ImportAsync(
        Guid ownerUserId,
        IReadOnlyList<ExternalFoodItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(ownerUserId));
        if (items.Count == 0)
            return [];

        var fdcIds = items.Select(item => (int?)item.FdcId).Distinct().ToArray();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var records = await strategy.ExecuteAsync<List<UserFoodItemRecord>>(async strategyCancellationToken =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(strategyCancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(
                dbContext, ownerUserId, strategyCancellationToken).ConfigureAwait(false);

            var existing = await dbContext.UserFoodItems.AsNoTracking()
                .Where(food => food.UserId == ownerUserId && fdcIds.Contains(food.FdcId))
                .ToListAsync(strategyCancellationToken)
                .ConfigureAwait(false);
            var byFdcId = existing
                .GroupBy(food => food.FdcId!.Value)
                .ToDictionary(group => group.Key, group => group.OrderBy(food => food.CreatedAt).First());

            var now = DateTime.UtcNow;
            var result = new List<UserFoodItemRecord>(items.Count);
            foreach (var item in items)
            {
                if (byFdcId.TryGetValue(item.FdcId, out var known))
                {
                    result.Add(known);
                    continue;
                }

                var record = new UserFoodItemRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = ownerUserId,
                    Name = item.Name.Trim(),
                    Calories = (decimal)item.CaloriesPer100Grams,
                    Protein = (decimal)item.ProteinPer100Grams,
                    Carbs = (decimal)item.CarbsPer100Grams,
                    Fats = (decimal)item.FatsPer100Grams,
                    FdcId = item.FdcId,
                    DataType = string.IsNullOrWhiteSpace(item.DataType) ? null : item.DataType.Trim(),
                    ServingSize = ReferenceGrams,
                    ServingSizeUnit = ReferenceUnit,
                    LastSyncedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.UserFoodItems.Add(record);
                byFdcId[item.FdcId] = record;
                result.Add(record);
            }

            await dbContext.SaveChangesAsync(strategyCancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(strategyCancellationToken).ConfigureAwait(false);
            return result;
        }, cancellationToken).ConfigureAwait(false);

        return records.Select(PostgresFoodCatalogStore.ToCandidate).ToList();
    }
}
