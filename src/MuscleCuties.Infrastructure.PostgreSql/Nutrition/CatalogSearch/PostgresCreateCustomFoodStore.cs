using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.CatalogSearch;

public sealed class PostgresCreateCustomFoodStore(PostgresDbContext dbContext) : ICreateCustomFoodStore
{
    public async Task<CustomFoodRecord> CreateAsync(
        Guid ownerUserId,
        CustomFoodNutrition nutrition,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(ownerUserId));

        var recordId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<CustomFoodRecord>(async strategyCancellationToken =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(strategyCancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(
                dbContext, ownerUserId, strategyCancellationToken).ConfigureAwait(false);

            var existing = await dbContext.UserFoodItems.AsNoTracking()
                .SingleOrDefaultAsync(food => food.UserId == ownerUserId && food.Id == recordId, strategyCancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                await transaction.CommitAsync(strategyCancellationToken).ConfigureAwait(false);
                return ToCustomFoodRecord(existing);
            }

            var record = new UserFoodItemRecord
            {
                Id = recordId,
                UserId = ownerUserId,
                Name = nutrition.Name,
                Calories = (decimal)nutrition.CaloriesPer100Grams,
                Protein = (decimal)nutrition.ProteinPer100Grams,
                Carbs = (decimal)nutrition.CarbsPer100Grams,
                Fats = (decimal)nutrition.FatsPer100Grams,
                // Optional nutrients are unknown until the user explicitly supplies them.
                DataType = "Custom",
                ServingSize = (decimal)nutrition.ServingAmount,
                ServingSizeUnit = nutrition.ServingUnit,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.UserFoodItems.Add(record);
            await dbContext.SaveChangesAsync(strategyCancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(strategyCancellationToken).ConfigureAwait(false);
            return ToCustomFoodRecord(record);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static CustomFoodRecord ToCustomFoodRecord(UserFoodItemRecord record) => new(
        record.Id, record.Name, (float)record.Calories!.Value, (float)record.Protein!.Value,
        (float)record.Carbs!.Value, (float)record.Fats!.Value, (float)record.ServingSize!.Value, record.ServingSizeUnit!);
}
