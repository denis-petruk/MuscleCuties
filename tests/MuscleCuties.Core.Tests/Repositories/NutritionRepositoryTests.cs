using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Tests.Repositories;

public class NutritionRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public NutritionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchFoodItemsAsync_MatchingQuery_ReturnsResults()
    {
        var repo = new NutritionRepository(_fixture.Db);
        await repo.AddAsync(new FoodItem { Name = "Chicken Breast", Calories = 165, Protein = 31, Carbs = 0, Fats = 3.6f });
        await repo.AddAsync(new FoodItem { Name = "Brown Rice", Calories = 216, Protein = 5, Carbs = 44, Fats = 1.8f });

        var results = await repo.SearchFoodItemsAsync("chicken");

        Assert.Single(results);
        Assert.Equal("Chicken Breast", results[0].Name);
    }

    [Fact]
    public async Task GetFoodLogsByDateAsync_OnDate_ReturnsLogs()
    {
        var repo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Apple", Calories = 52, Protein = 0.3f, Carbs = 14, Fats = 0.2f };
        await repo.AddAsync(item);

        var today = DateTime.UtcNow.Date;
        var log = new FoodLog { UserId = 1, FoodItemId = item.Id, Date = today, Grams = 150, MealType = MealType.Snack };
        await repo.AddFoodLogAsync(log);

        var results = await repo.GetFoodLogsByDateAsync(1, today);

        Assert.Single(results);
    }

    [Fact]
    public async Task DeleteFoodLogAsync_ExistingLog_RemovesFromDb()
    {
        var repo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Banana", Calories = 89, Protein = 1.1f, Carbs = 23, Fats = 0.3f };
        await repo.AddAsync(item);

        var log = new FoodLog { UserId = 1, FoodItemId = item.Id, Date = DateTime.UtcNow.Date, Grams = 100, MealType = MealType.Breakfast };
        await repo.AddFoodLogAsync(log);

        await repo.DeleteFoodLogAsync(log);

        var results = await repo.GetFoodLogsByDateAsync(1, DateTime.UtcNow.Date);
        Assert.DoesNotContain(results, l => l.Id == log.Id);
    }
}
