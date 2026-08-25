using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;

namespace MuscleCuties.Core.Tests.Repositories.Nutrition;

public class NutritionRepositoryTests : IDisposable
{
    private readonly DatabaseFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

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
    public async Task SearchFoodItemsAsync_MultipleWords_MatchesAllTokens()
    {
        var repo = new NutritionRepository(_fixture.Db);
        await repo.AddAsync(new FoodItem { Name = "Chicken Breast Raw", Calories = 120, Protein = 22.5f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await repo.AddAsync(new FoodItem { Name = "Chicken Thigh Raw", Calories = 144, Protein = 19.6f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        var results = await repo.SearchFoodItemsAsync("chicken breast");

        var result = Assert.Single(results);
        Assert.Equal("Chicken Breast Raw", result.Name);
    }

    [Fact]
    public async Task SearchFoodItemsAsync_PluralQuery_MatchesSingularFoodName()
    {
        var repo = new NutritionRepository(_fixture.Db);
        await repo.AddAsync(new FoodItem { Name = "Oatmeal Dry", Calories = 389, Protein = 16.9f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await repo.AddAsync(new FoodItem { Name = "Brown Rice", Calories = 216, Protein = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        var results = await repo.SearchFoodItemsAsync("oats");

        var result = Assert.Single(results);
        Assert.Equal("Oatmeal Dry", result.Name);
    }

    [Fact]
    public async Task SearchFoodItemsAsync_PunctuationQuery_MatchesFoodName()
    {
        var repo = new NutritionRepository(_fixture.Db);
        await repo.AddAsync(new FoodItem { Name = "Oat Milk", Calories = 45, Protein = 1.2f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await repo.AddAsync(new FoodItem { Name = "Almond Butter", Calories = 614, Protein = 21, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        var results = await repo.SearchFoodItemsAsync("oat-milk");

        var result = Assert.Single(results);
        Assert.Equal("Oat Milk", result.Name);
    }

    [Fact]
    public async Task SearchFoodItemsAsync_BrandedFood_MatchesBrandAndUpc()
    {
        var repo = new NutritionRepository(_fixture.Db);
        await repo.AddAsync(new FoodItem
        {
            Name = "Protein bar",
            Calories = 220,
            Protein = 20,
            DataType = "Branded",
            BrandOwner = "Acme Nutrition",
            GtinUpc = "123456789",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await repo.AddAsync(new FoodItem
        {
            Name = "Protein bar",
            Calories = 200,
            Protein = 18,
            DataType = "Branded",
            BrandOwner = "Other Nutrition",
            GtinUpc = "987654321",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var brandResults = await repo.SearchFoodItemsAsync("acme protein");
        var upcResults = await repo.SearchFoodItemsAsync("123456789");

        var brandResult = Assert.Single(brandResults);
        Assert.Equal("Acme Nutrition", brandResult.BrandOwner);
        var upcResult = Assert.Single(upcResults);
        Assert.Equal("123456789", upcResult.GtinUpc);
    }

    [Fact]
    public async Task AddLoggedMealAsync_WithEntries_CanBeRetrievedByDate()
    {
        var repo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Apple", Calories = 52, Protein = 0.3f, Carbs = 14, Fats = 0.2f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await repo.AddAsync(item);

        var today = DateTime.UtcNow.Date;
        var loggedAt = today.AddHours(14).AddMinutes(30);
        var meal = new LoggedMeal
        {
            UserId = 1,
            Date = today,
            LoggedAt = loggedAt,
            MealType = MealType.Snack,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealEntry { FoodItemId = item.Id, Grams = 150 }]
        };
        await repo.AddLoggedMealAsync(meal);

        var results = await repo.GetLoggedMealsByDateAsync(1, today);

        Assert.Single(results);
        Assert.Single(results[0].Entries);
        Assert.Equal(loggedAt, results[0].LoggedAt);
    }

    [Fact]
    public async Task GetLoggedMealsByDateAsync_OrdersMealsByLoggedTimeAscending()
    {
        var repo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Carrot", Calories = 41, Protein = 0.9f, Carbs = 9.6f, Fats = 0.2f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await repo.AddAsync(item);

        var today = DateTime.UtcNow.Date;
        await repo.AddLoggedMealAsync(new LoggedMeal
        {
            UserId = 1,
            Date = today,
            LoggedAt = today.AddHours(9),
            MealType = MealType.Breakfast,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealEntry { FoodItemId = item.Id, Grams = 100 }]
        });
        await repo.AddLoggedMealAsync(new LoggedMeal
        {
            UserId = 1,
            Date = today,
            LoggedAt = today.AddHours(18),
            MealType = MealType.Dinner,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealEntry { FoodItemId = item.Id, Grams = 100 }]
        });

        var results = await repo.GetLoggedMealsByDateAsync(1, today);

        Assert.True(results[0].LoggedAt <= results[1].LoggedAt);
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
            LoggedAt = DateTime.UtcNow.Date.AddHours(8),
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
