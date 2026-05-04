using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Tests.Repositories;

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
        await repo.AddAsync(new FoodItem { Name = "Chicken Breast", Calories = 165, Protein = 31, Carbs = 0, Fats = 3.6f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await repo.AddAsync(new FoodItem { Name = "Brown Rice", Calories = 216, Protein = 5, Carbs = 44, Fats = 1.8f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        var results = await repo.SearchFoodItemsAsync("chicken");

        Assert.Single(results);
        Assert.Equal("Chicken Breast", results[0].Name);
    }

    [Fact]
    public async Task AddLoggedMealAsync_WithEntries_CanBeRetrievedByDate()
    {
        var repo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Apple", Calories = 52, Protein = 0.3f, Carbs = 14, Fats = 0.2f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await repo.AddAsync(item);

        var today = DateTime.UtcNow.Date;
        var meal = new LoggedMeal
        {
            UserId = 1,
            Date = today,
            MealType = MealType.Snack,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealEntry { FoodItemId = item.Id, Grams = 150 }]
        };
        await repo.AddLoggedMealAsync(meal);

        var results = await repo.GetLoggedMealsByDateAsync(1, today);

        Assert.Single(results);
        Assert.Single(results[0].Entries);
    }

    [Fact]
    public async Task DeleteLoggedMealAsync_RemovesFromDb()
    {
        var repo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Banana", Calories = 89, Protein = 1.1f, Carbs = 23, Fats = 0.3f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await repo.AddAsync(item);

        var meal = new LoggedMeal
        {
            UserId = 1,
            Date = DateTime.UtcNow.Date,
            MealType = MealType.Breakfast,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealEntry { FoodItemId = item.Id, Grams = 100 }]
        };
        await repo.AddLoggedMealAsync(meal);
        await repo.DeleteLoggedMealAsync(meal);

        var results = await repo.GetLoggedMealsByDateAsync(1, DateTime.UtcNow.Date);
        Assert.DoesNotContain(results, m => m.Id == meal.Id);
    }
}