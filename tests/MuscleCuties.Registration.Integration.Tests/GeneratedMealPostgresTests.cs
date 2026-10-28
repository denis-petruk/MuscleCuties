using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Overview;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.CatalogSearch;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Targets;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class GeneratedMealPostgresTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string DisposableDatabasePrefix = "mc_test_nutrition_";
    private const float NutrientReferenceGrams = 100f;
    private const float Tolerance = 0.01f;
    private const int DinnerHour = 18;
    private const float LargerMealScale = 3f;
    private const float OilCaloriesPer100Grams = 900f;
    private const decimal SyntheticFiber = 7m;

    [Fact]
    public async Task GeneratedOilAmountSurvivesMealLoggingAndReadback()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
        Assert.Equal("127.0.0.1", connection.Host);
        Assert.StartsWith(DisposableDatabasePrefix, connection.Database, StringComparison.Ordinal);
        await using var database = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var date = DateOnly.FromDateTime(now);
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord
        {
            Id = userId, Email = $"nutrition-oil-{userId:N}@example.invalid", Username = $"oil_{userId:N}",
            CredentialType = "Password", PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now,
            IsOnboardingComplete = true
        });
        await database.SaveChangesAsync();
        try
        {
            var store = new PostgresNutritionOverviewStore(database, new PostgresNutritionTargetProjectionStore(database));
            var customOil = await store.CreateCustomFoodAsync(userId,
                new CreateCustomFoodRequest("Olive Oil", NutrientReferenceGrams, "g", OilCaloriesPer100Grams,
                    0f, 0f, NutrientReferenceGrams));
            var customOilRecord = await database.UserFoodItems.SingleAsync(food => food.UserId == userId);
            var customOilId = customOilRecord.Id;
            customOilRecord.Fiber = SyntheticFiber;
            await database.SaveChangesAsync();
            var plan = await store.GetDayMealPlanAsync(userId, date, "Savoury");
            var dinner = Assert.Single(plan, item => item.MealType == "Dinner");
            var suggestion = Assert.IsType<MealSuggestionDto>(dinner.Suggestion);
            var oil = Assert.Single(suggestion.Components, component => component.Name == "Olive Oil");
            Assert.InRange(oil.Grams, 0f, PortionCalculationRule.MaxOilGrams);
            var ingredients = suggestion.Components.Select(component => new MealIngredientInputDto(
                component.FoodItemId, component.Name, component.Grams, component.Grams, "g", component.Calories,
                component.Protein, component.Carbs, component.Fats, string.Empty)).ToArray();
            await store.LogMealAsync(userId, new LogMealRequest(date, "Dinner", TimeSpan.FromHours(DinnerHour),
                ingredients, suggestion.ConceptName));
            var overview = await store.GetOverviewAsync(userId, date, "Savoury");
            var loggedMeal = Assert.Single(overview.Meals, item => item.LoggedMealId != 0);
            var readback = await store.GetMealIngredientsAsync(userId, loggedMeal.LoggedMealId);
            var savedOil = Assert.Single(readback, ingredient => ingredient.Name == oil.Name);
            Assert.Equal(oil.Grams, savedOil.Grams);
            Assert.InRange(savedOil.Grams, 0f, PortionCalculationRule.MaxOilGrams);
            AssertClose(savedOil.Grams, savedOil.Fats * savedOil.Grams / NutrientReferenceGrams);
            AssertClose(readback.Sum(item => item.Calories * item.Grams / NutrientReferenceGrams), loggedMeal.Calories);
            AssertClose(readback.Sum(item => item.Protein * item.Grams / NutrientReferenceGrams), loggedMeal.Protein);
            AssertClose(readback.Sum(item => item.Carbs * item.Grams / NutrientReferenceGrams), loggedMeal.Carbs);
            AssertClose(readback.Sum(item => item.Fats * item.Grams / NutrientReferenceGrams), loggedMeal.Fats);
            AssertClose(suggestion.Calories, loggedMeal.Calories);
            AssertClose(suggestion.Protein, loggedMeal.Protein);
            AssertClose(suggestion.Carbs, loggedMeal.Carbs);
            AssertClose(suggestion.Fats, loggedMeal.Fats);

            var largerMeal = await store.RefreshDayMealAsync(userId, dinner with
            {
                TargetCalories = dinner.TargetCalories * LargerMealScale,
                TargetProtein = dinner.TargetProtein * LargerMealScale,
                TargetCarbs = dinner.TargetCarbs * LargerMealScale,
                TargetFats = dinner.TargetFats * LargerMealScale,
                Suggestion = null
            }, "Savoury");
            var largerSuggestion = Assert.IsType<MealSuggestionDto>(largerMeal.Suggestion);
            await store.LogMealAsync(userId, new LogMealRequest(date, "Dinner", TimeSpan.FromHours(DinnerHour),
                largerSuggestion.Components.Select(component => new MealIngredientInputDto(component.FoodItemId,
                    component.Name, component.Grams, component.Grams, "g", component.Calories, component.Protein,
                    component.Carbs, component.Fats, string.Empty)).ToArray(), largerSuggestion.ConceptName));
            var afterSecondLog = await store.GetOverviewAsync(userId, date, "Savoury");
            var firstHistory = Assert.Single(afterSecondLog.Meals, item => item.LoggedMealId == loggedMeal.LoggedMealId);
            var secondHistory = Assert.Single(afterSecondLog.Meals,
                item => item.LoggedMealId != 0 && item.LoggedMealId != loggedMeal.LoggedMealId);
            AssertClose(suggestion.Calories, firstHistory.Calories);
            AssertClose(suggestion.Fats, firstHistory.Fats);
            AssertClose(largerSuggestion.Calories, secondHistory.Calories);
            AssertClose(largerSuggestion.Protein, secondHistory.Protein);
            AssertClose(largerSuggestion.Carbs, secondHistory.Carbs);
            AssertClose(largerSuggestion.Fats, secondHistory.Fats);
            Assert.DoesNotContain(await database.LoggedMealEntries.Where(entry => entry.UserId == userId).ToListAsync(),
                entry => entry.UserFoodItemId == customOilId);
            var searchResults = await new PostgresFoodCatalogStore(database).SearchCandidatesAsync("Olive Oil", userId);
            var customResult = Assert.Single(searchResults, candidate => candidate.IsCustom);
            Assert.Equal(customOil.FoodItemId, customResult.Id);
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(actual, expected - Tolerance, expected + Tolerance);
}
