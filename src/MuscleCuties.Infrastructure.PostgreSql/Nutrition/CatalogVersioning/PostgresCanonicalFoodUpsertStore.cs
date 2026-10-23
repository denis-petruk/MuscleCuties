using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Models.Operations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.CatalogVersioning;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.CatalogVersioning;

/// <summary>Atomic canonical nutrient upsert with a snapshot of values replaced by a change.</summary>
public sealed class PostgresCanonicalFoodUpsertStore(PostgresDbContext dbContext) : ICanonicalFoodUpsertStore
{
    public async Task<CanonicalFoodUpsertResult> UpsertAsync(
        CanonicalFoodUpsertInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.SeedCode) || string.IsNullOrWhiteSpace(input.Name) ||
            string.IsNullOrWhiteSpace(input.ChangeSource) || input.Nutrients is null || !input.Nutrients.IsValid)
            throw new ArgumentException("A seed code, name, change source, and valid nutrients are required.", nameof(input));

        var seedCode = input.SeedCode.Trim();
        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => UpsertTransactionalAsync(input, seedCode, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<CanonicalFoodUpsertResult> UpsertTransactionalAsync(
        CanonicalFoodUpsertInput input,
        string seedCode,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        // Serialize changes to one existing canonical row before capturing its prior values.
        var matching = await dbContext.CatalogFoodItems
            .FromSqlInterpolated($"SELECT * FROM catalog.food_items WHERE seed_code = {seedCode} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var food = matching.SingleOrDefault();
        var created = food is null;
        var changed = false;
        var now = DateTime.UtcNow;
        if (created)
        {
            food = new FoodItem
            {
                SeedCode = seedCode,
                Name = input.Name.Trim(),
                FdcId = input.FdcId,
                CreatedAt = now,
                UpdatedAt = now
            };
            ApplyNutrients(food, input.Nutrients);
            dbContext.CatalogFoodItems.Add(food);
        }
        else
        {
            var previous = ReadNutrients(food!);
            changed = input.Nutrients.HasChangedFrom(previous);
            if (changed)
            {
                dbContext.FoodItemVersions.Add(new FoodItemVersionRecord
                {
                    FoodItemId = food!.Id,
                    NutrientJson = JsonSerializer.Serialize(previous),
                    VersionedAtUtc = now,
                    ChangeSource = input.ChangeSource.Trim()
                });
                ApplyNutrients(food, input.Nutrients);
            }
            food!.Name = input.Name.Trim();
            food.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CanonicalFoodUpsertResult(food!.Id, created, changed);
    }

    private static FoodCatalogNutrientProfile ReadNutrients(FoodItem food) => new(
        food.Calories, food.Protein, food.Carbs, food.Fats, food.Fiber, food.Iron, food.VitaminB12,
        food.VitaminC, food.VitaminD, food.VitaminA, food.VitaminB6, food.Folate, food.Calcium,
        food.Magnesium, food.Zinc, food.Potassium);

    private static void ApplyNutrients(FoodItem food, FoodCatalogNutrientProfile nutrients)
    {
        food.Calories = nutrients.Calories;
        food.Protein = nutrients.Protein;
        food.Carbs = nutrients.Carbs;
        food.Fats = nutrients.Fats;
        food.Fiber = nutrients.Fiber;
        food.Iron = nutrients.Iron;
        food.VitaminB12 = nutrients.VitaminB12;
        food.VitaminC = nutrients.VitaminC;
        food.VitaminD = nutrients.VitaminD;
        food.VitaminA = nutrients.VitaminA;
        food.VitaminB6 = nutrients.VitaminB6;
        food.Folate = nutrients.Folate;
        food.Calcium = nutrients.Calcium;
        food.Magnesium = nutrients.Magnesium;
        food.Zinc = nutrients.Zinc;
        food.Potassium = nutrients.Potassium;
    }
}
