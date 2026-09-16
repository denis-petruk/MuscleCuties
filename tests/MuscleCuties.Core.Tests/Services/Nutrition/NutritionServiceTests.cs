using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Inputs;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class NutritionServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public NutritionServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private NutritionService CreateService()
    {
        return new NutritionService(
            new UserRepository(_fixture.Db),
            new NutritionRepository(_fixture.Db),
            new CalorieCalculator());
    }

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
            WeightGoalPace = WeightGoalPace.Steady,
            TrainingExperienceLevel = TrainingExperienceLevel.Intermediate,
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
    public async Task GetDailyPlanAsync_WithProfile_ReturnsPlannerBreakdown()
    {
        var user = await SeedUserWithProfileAsync("nut-plan@test.com", UserGoal.Strength);

        var plan = await CreateService().GetDailyPlanAsync(
            user.Id,
            CyclePhase.Luteal,
            new DateTime(2026, 8, 11));

        Assert.True(plan.Bmr > 0f);
        Assert.True(plan.Tdee > plan.Bmr);
        Assert.True(plan.Calories > 0f);
        Assert.True(plan.Protein > 0f);
        Assert.Equal(150f, plan.PhaseAdjustment);
        Assert.Equal(4, plan.Meals.Count);
        Assert.Equal(CyclePhase.Luteal, plan.Phase);
    }

    [Fact]
    public async Task GetConsumedTotalsAsync_NoLogs_ReturnsZero()
    {
        var result = await CreateService().GetConsumedTotalsAsync(9999, DateTime.UtcNow);

        Assert.Equal(0f, result.Calories);
        Assert.Equal(0f, result.Protein);
        Assert.Equal(0f, result.Carbs);
        Assert.Equal(0f, result.Fats);
    }

    [Fact]
    public async Task GetConsumedTotalsAsync_WithLoggedMeal_ReturnsSummedCalories()
    {
        var user = await SeedUserWithProfileAsync("nut2@test.com");
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem
        {
            Name = "Oats",
            Calories = 389f,
            Protein = 17f,
            Carbs = 66f,
            Fats = 7f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await nutritionRepo.AddAsync(item);

        await nutritionRepo.AddLoggedMealAsync(new LoggedMeal
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.Date,
            LoggedAt = DateTime.UtcNow.Date.AddHours(8),
            MealType = MealType.Breakfast,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealIngredient { FoodItemId = item.Id, Grams = 100f }]
        });

        var result = await CreateService().GetConsumedTotalsAsync(user.Id, DateTime.UtcNow);
        Assert.Equal(389f, result.Calories, 0);
    }

    [Fact]
    public async Task GetConsumedTotalsAsync_WithLoggedMeal_ReturnsSummedMacros()
    {
        var user = await SeedUserWithProfileAsync("nut3@test.com");
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem
        {
            Name = "Eggs",
            Calories = 143f,
            Protein = 13f,
            Carbs = 1f,
            Fats = 10f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await nutritionRepo.AddAsync(item);

        await nutritionRepo.AddLoggedMealAsync(new LoggedMeal
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.Date,
            LoggedAt = DateTime.UtcNow.Date.AddHours(8),
            MealType = MealType.Breakfast,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealIngredient { FoodItemId = item.Id, Grams = 100f }]
        });

        var result = await CreateService().GetConsumedTotalsAsync(user.Id, DateTime.UtcNow);
        Assert.Equal(13f, result.Protein, 0);
        Assert.Equal(1f, result.Carbs, 0);
        Assert.Equal(10f, result.Fats, 0);
    }

    [Fact]
    public async Task GetConsumedTotalsAsync_WithLoggedMeal_ReturnsCaloriesAndMacrosTogether()
    {
        var user = await SeedUserWithProfileAsync("nut-totals@test.com");
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem
        {
            Name = "Yogurt",
            Calories = 90f,
            Protein = 8f,
            Carbs = 9f,
            Fats = 2f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await nutritionRepo.AddAsync(item);

        await nutritionRepo.AddLoggedMealAsync(new LoggedMeal
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.Date,
            LoggedAt = DateTime.UtcNow.Date.AddHours(10),
            MealType = MealType.Snack,
            CreatedAt = DateTime.UtcNow,
            Entries = [new LoggedMealIngredient { FoodItemId = item.Id, Grams = 150f }]
        });

        var totals = await CreateService().GetConsumedTotalsAsync(user.Id, DateTime.UtcNow);

        Assert.Equal(135f, totals.Calories, 0);
        Assert.Equal(12f, totals.Protein, 0);
        Assert.Equal(13.5f, totals.Carbs, 1);
        Assert.Equal(3f, totals.Fats, 0);
    }

    [Fact]
    public async Task SearchFoodItemsAsync_FiltersFoodsMissingCalories()
    {
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        await nutritionRepo.AddAsync(new FoodItem
        {
            Name = "Brokenfilter oats complete",
            Calories = 389f,
            Protein = 16.9f,
            Carbs = 66.3f,
            Fats = 6.9f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await nutritionRepo.AddAsync(new FoodItem
        {
            Name = "Brokenfilter oats missing calories",
            Calories = 0f,
            Protein = 16.9f,
            Carbs = 66.3f,
            Fats = 6.9f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var results = await CreateService().SearchFoodItemsAsync("brokenfilter");

        var result = Assert.Single(results);
        Assert.Equal("Brokenfilter oats complete", result.Name);
    }

    [Fact]
    public async Task SearchFoodItemsAsync_WhenLocalOnly_ReturnsRequestedPage()
    {
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        for (var i = 1; i <= 20; i++)
            await nutritionRepo.AddAsync(new FoodItem
            {
                Name = $"Localpage food {i}",
                Calories = 100f + i,
                Protein = 10f,
                Carbs = 20f,
                Fats = 5f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var results = await CreateService().SearchFoodItemsAsync("localpage", 15, 2);

        Assert.Equal(5, results.Count);
        Assert.Contains(results, food => food.Name == "Localpage food 16");
        Assert.Contains(results, food => food.Name == "Localpage food 20");
    }

    [Fact]
    public async Task CreateCustomFoodAsync_SavesNutritionPerServingAsPer100g()
    {
        var food = await CreateService().CreateCustomFoodAsync(new CustomFoodInput(
            "Protein pudding",
            50f,
            "g",
            120f,
            15f,
            10f,
            2f));

        Assert.True(food.IsCustom);
        Assert.Equal("Custom", food.DataType);
        Assert.Equal(50f, food.ServingSize);
        Assert.Equal("g", food.ServingSizeUnit);
        Assert.Equal(240f, food.Calories);
        Assert.Equal(30f, food.Protein);

        var saved = await _fixture.Db.FoodItems.FindAsync(food.Id);
        Assert.NotNull(saved);
        Assert.Equal("Protein pudding", saved.Name);
    }

    [Fact]
    public void FatSolubleVitaminAbsorption_CountsNutrientOnlyWhenEnoughFatIsNearby()
    {
        var carrot = new FoodItem
        {
            Name = "Carrot",
            VitaminA = 835f,
            Fats = 0.2f
        };
        var oliveOil = new FoodItem
        {
            Name = "Olive oil",
            Fats = 100f
        };
        var carrotMeal = BuildMealWithEntry(DateTime.Today.AddHours(8), carrot, 100f);
        var nearbyOilMeal = BuildMealWithEntry(DateTime.Today.AddHours(9).AddMinutes(45), oliveOil, 5f);
        var lateOilMeal = BuildMealWithEntry(DateTime.Today.AddHours(10).AddMinutes(5), oliveOil, 5f);

        var absorbed = FatSolubleVitaminAbsorption.SumAbsorbableNutrient(
            [carrotMeal],
            [carrotMeal, nearbyOilMeal],
            food => food.VitaminA);
        var notAbsorbed = FatSolubleVitaminAbsorption.SumAbsorbableNutrient(
            [carrotMeal],
            [carrotMeal, lateOilMeal],
            food => food.VitaminA);

        Assert.Equal(835f, absorbed);
        Assert.Equal(0f, notAbsorbed);
    }

    [Fact]
    public async Task LogMealAsync_StoresMealWithExactLoggedTime()
    {
        var user = await SeedUserWithProfileAsync("nut-log@test.com");
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem
        {
            Name = "Olive oil",
            Calories = 884f,
            Fats = 100f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await nutritionRepo.AddAsync(item);

        var loggedAt = DateTime.Today.AddHours(12).AddMinutes(30);

        await CreateService().LogMealAsync(
            user.Id,
            [new MealIngredientInput(item.Id, 10f)],
            MealType.Snack,
            loggedAt);

        var meals = await nutritionRepo.GetLoggedMealsByDateAsync(user.Id, loggedAt.Date);
        var meal = Assert.Single(meals);
        Assert.Equal(loggedAt, meal.LoggedAt);
        Assert.Equal(MealType.Snack, meal.MealType);
        Assert.Equal(10f, meal.Entries.Single().Grams);
    }

    private static LoggedMeal BuildMealWithEntry(DateTime loggedAt, FoodItem food, float grams)
    {
        return new LoggedMeal
        {
            Date = loggedAt.Date,
            LoggedAt = loggedAt,
            Entries =
            [
                new LoggedMealIngredient
                {
                    FoodItem = food,
                    Grams = grams
                }
            ]
        };
    }

    [Fact]
    public async Task LogMealAsync_StoresOneMealWithMultipleIngredients()
    {
        var user = await SeedUserWithProfileAsync("nut-meal@test.com");
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var oats = new FoodItem
        {
            Name = "Oats",
            Calories = 389f,
            Protein = 16.9f,
            Carbs = 66.3f,
            Fats = 6.9f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var oil = new FoodItem
        {
            Name = "Olive oil",
            Calories = 884f,
            Fats = 100f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await nutritionRepo.AddAsync(oats);
        await nutritionRepo.AddAsync(oil);

        var loggedAt = DateTime.Today.AddHours(8);

        await CreateService().LogMealAsync(
            user.Id,
            [new MealIngredientInput(oats.Id, 40f), new MealIngredientInput(oil.Id, 10f)],
            MealType.Breakfast,
            loggedAt);

        var meal = Assert.Single(await nutritionRepo.GetLoggedMealsByDateAsync(user.Id, loggedAt.Date));
        Assert.Equal(MealType.Breakfast, meal.MealType);
        Assert.Equal(loggedAt, meal.LoggedAt);
        Assert.Equal(2, meal.Entries.Count);
        Assert.Contains(meal.Entries, e => e.FoodItemId == oats.Id && e.Grams == 40f);
        Assert.Contains(meal.Entries, e => e.FoodItemId == oil.Id && e.Grams == 10f);
        Assert.All(meal.Entries, entry => Assert.NotNull(entry.FoodItem));
    }

    [Fact]
    public async Task LogMealAsync_WhenUserMissing_ThrowsFriendlyErrorBeforeSaving()
    {
        var nutritionRepo = new NutritionRepository(_fixture.Db);
        var item = new FoodItem
        { Name = "Carrot", Calories = 41f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await nutritionRepo.AddAsync(item);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().LogMealAsync(
                999999,
                [new MealIngredientInput(item.Id, 100f)],
                MealType.Snack,
                DateTime.Today.AddHours(10)));

        Assert.Equal("Current user no longer exists. Please sign in again.", ex.Message);
    }
}
