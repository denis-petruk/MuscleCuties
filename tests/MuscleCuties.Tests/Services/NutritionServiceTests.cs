using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Tests.Services;

public class NutritionServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public NutritionServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private NutritionService CreateService() =>
        new NutritionService(
            new UserRepository(_fixture.Db),
            new NutritionRepository(_fixture.Db),
            new CalorieCalculator(new CyclePhaseCalculator()));

    private async Task<User> SeedUserWithProfileAsync(string email, UserGoal goal = UserGoal.MaintainHealth)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();

        var profile = new UserProfile
        {
            UserId = user.Id,
            Name = "Test",
            DateOfBirth = new DateTime(1995, 6, 15),
            Height = 165f,
            Weight = 65f,
            Goal = goal,
            WorkoutDaysPerWeek = 4,
            CycleLength = 28,
            UpdatedAt = DateTime.UtcNow
        };
        await _fixture.Db.UserProfiles.AddAsync(profile);
        await _fixture.Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CalculateDailyTargetsAsync_NoProfile_ReturnsDefaults()
    {
        var (cal, pro, _, _) = await CreateService().CalculateDailyTargetsAsync(9999, CyclePhase.Follicular);
        Assert.Equal(2000f, cal);
        Assert.Equal(120f, pro);
    }

    [Fact]
    public async Task CalculateDailyTargetsAsync_WithProfile_ReturnsPositiveValues()
    {
        var user = await SeedUserWithProfileAsync("nut1@test.com");
        var (cal, pro, carbs, fats) = await CreateService().CalculateDailyTargetsAsync(user.Id, CyclePhase.Follicular);
        Assert.True(cal > 0);
        Assert.True(pro > 0);
        Assert.True(carbs > 0);
        Assert.True(fats > 0);
    }

    [Fact]
    public async Task GetConsumedCaloriesAsync_NoLogs_ReturnsZero()
    {
        Assert.Equal(0f, await CreateService().GetConsumedCaloriesAsync(9999, DateTime.UtcNow));
    }

    [Fact]
    public async Task GetConsumedCaloriesAsync_WithLoggedMeal_ReturnsSummedCalories()
    {
        var user = await SeedUserWithProfileAsync("nut2@test.com");
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Oats", Calories = 389f, Protein = 17f, Carbs = 66f, Fats = 7f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await nutritionRepo.AddAsync(item);

        await nutritionRepo.AddLoggedMealAsync(new LoggedMeal
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.Date,
            MealType = MealType.Breakfast,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealEntry { FoodItemId = item.Id, Grams = 100f }]
        });

        var result = await CreateService().GetConsumedCaloriesAsync(user.Id, DateTime.UtcNow);
        Assert.Equal(389f, result, precision: 0);
    }

    [Fact]
    public async Task GetConsumedMacrosAsync_WithLoggedMeal_ReturnsSummedMacros()
    {
        var user = await SeedUserWithProfileAsync("nut3@test.com");
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem { Name = "Eggs", Calories = 143f, Protein = 13f, Carbs = 1f, Fats = 10f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await nutritionRepo.AddAsync(item);

        await nutritionRepo.AddLoggedMealAsync(new LoggedMeal
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.Date,
            MealType = MealType.Breakfast,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealEntry { FoodItemId = item.Id, Grams = 100f }]
        });

        var (pro, carbs, fats) = await CreateService().GetConsumedMacrosAsync(user.Id, DateTime.UtcNow);
        Assert.Equal(13f, pro, precision: 0);
        Assert.Equal(1f, carbs, precision: 0);
        Assert.Equal(10f, fats, precision: 0);
    }
}