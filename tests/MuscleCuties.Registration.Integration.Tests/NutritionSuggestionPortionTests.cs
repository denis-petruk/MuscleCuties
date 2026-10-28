using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Targets;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class NutritionSuggestionPortionTests
{
    private const float SmallMealCalories = 300f;
    private const float MediumMealCalories = 600f;
    private const float LargeMealCalories = 1200f;
    private const float TargetProtein = 45f;
    private const float TargetCarbs = 70f;
    private const float TargetFats = 40f;
    private const float LowFatBudget = 1f;
    private const float DailyCalories = 2400f;
    private const float DailyProtein = 150f;
    private const float DailyCarbs = 250f;
    private const float DailyFats = 70f;
    private const float Tolerance = 0.001f;
    private const float NutrientReferenceGrams = 100f;

    [Theory]
    [InlineData("Breakfast", SmallMealCalories)]
    [InlineData("Breakfast", MediumMealCalories)]
    [InlineData("Breakfast", LargeMealCalories)]
    [InlineData("Lunch", SmallMealCalories)]
    [InlineData("Lunch", MediumMealCalories)]
    [InlineData("Lunch", LargeMealCalories)]
    [InlineData("Dinner", SmallMealCalories)]
    [InlineData("Dinner", MediumMealCalories)]
    [InlineData("Dinner", LargeMealCalories)]
    public async Task RefreshCapsEveryCatalogOilAndPreservesMealTargets(string mealType, float calories)
    {
        var store = CreateStore();
        var item = new DayMealPlanItemDto(mealType, calories, TargetProtein, TargetCarbs, TargetFats,
            DailyCalories, false, null);
        var first = await store.RefreshDayMealAsync(Guid.NewGuid(), item, "Savoury");
        var second = await store.RefreshDayMealAsync(Guid.NewGuid(), first, "Savoury");
        Assert.NotEqual(first.Suggestion!.ConceptName, second.Suggestion!.ConceptName);
        AssertPortions(first);
        AssertPortions(second);
        Assert.Contains(first.Suggestion.Components, IsOil);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(LowFatBudget)]
    public async Task OilRespectsRemainingFatBudget(float fats)
    {
        var item = new DayMealPlanItemDto("Dinner", SmallMealCalories, TargetProtein, TargetCarbs, fats,
            DailyCalories, false, null);
        var result = await CreateStore().RefreshDayMealAsync(Guid.NewGuid(), item, "Savoury");
        AssertPortions(result);
        if (fats == 0f)
            Assert.DoesNotContain(result.Suggestion!.Components, IsOil);
        else
            Assert.InRange(Assert.Single(result.Suggestion!.Components, IsOil).Grams, 0f, fats);
    }

    [Theory]
    [InlineData(SmallMealCalories)]
    [InlineData(MediumMealCalories)]
    [InlineData(LargeMealCalories)]
    public async Task AllDinnerConceptsIncludeCappedOliveAndSesameOil(float calories)
    {
        var shares = MealDistributionRule.GetShares(BreakfastPreference.Savoury);
        var dailyCalories = calories / shares.Dinner;
        var store = new PostgresNutritionOverviewStore(null!, new SyntheticTargetStore(dailyCalories));
        var suggestions = await store.GetMealSuggestionsAsync(Guid.NewGuid(), "Dinner", "Savoury");
        Assert.Contains(suggestions.SelectMany(suggestion => suggestion.Components), component => component.Name == "Sesame Oil");
        Assert.Contains(suggestions.SelectMany(suggestion => suggestion.Components), component => component.Name == "Olive Oil");
        foreach (var suggestion in suggestions)
            AssertPortions(new DayMealPlanItemDto("Dinner", calories, DailyProtein * shares.Dinner,
                DailyCarbs * shares.Dinner, DailyFats * shares.Dinner, dailyCalories, false, suggestion));
    }

    [Theory]
    [InlineData("Sweet", "Berry Protein Oatmeal", "Greek Yogurt Parfait")]
    [InlineData("Savoury", "Scrambled Eggs with Toast", "Tofu Scramble with Vegetables")]
    public async Task BreakfastPreferenceUsesMatchingCatalogAndPersonalTargets(
        string preference, string firstName, string secondName)
    {
        var userId = Guid.NewGuid();
        var source = new SyntheticTargetStore();
        var store = new PostgresNutritionOverviewStore(null!, source);
        var suggestions = await store.GetMealSuggestionsAsync(userId, "Breakfast", preference);
        Assert.Equal(userId, source.LastUserId);
        Assert.Equal(new[] { firstName, secondName }, suggestions.Select(item => item.ConceptName));
        var plan = await store.GetDayMealPlanAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), preference);
        var breakfast = Assert.Single(plan, item => item.MealType == "Breakfast");
        var shares = MealDistributionRule.GetShares(Enum.Parse<BreakfastPreference>(preference));
        AssertClose(DailyCalories * shares.Breakfast, breakfast.TargetCalories);
        AssertClose(DailyCalories, plan.Sum(item => item.TargetCalories));
        AssertClose(DailyProtein, plan.Sum(item => item.TargetProtein));
        AssertClose(DailyCarbs, plan.Sum(item => item.TargetCarbs));
        AssertClose(DailyFats, plan.Sum(item => item.TargetFats));
        Assert.All(plan, AssertPortions);
        foreach (var suggestion in suggestions)
            AssertPortions(breakfast with { Suggestion = suggestion });

        var excluded = await store.GetMealSuggestionsAsync(userId, "Breakfast", preference,
            new HashSet<string> { firstName });
        Assert.Equal(secondName, Assert.Single(excluded).ConceptName);
    }

    private static PostgresNutritionOverviewStore CreateStore() => new(null!, new SyntheticTargetStore());

    private static bool IsOil(MealSuggestionComponentDto item) => item.Name.EndsWith(" Oil", StringComparison.Ordinal);

    private static void AssertPortions(DayMealPlanItemDto item)
    {
        var suggestion = Assert.IsType<MealSuggestionDto>(item.Suggestion);
        Assert.NotEmpty(suggestion.Components);
        AssertClose(item.TargetCalories, suggestion.Calories);
        AssertClose(item.TargetProtein, suggestion.Protein);
        AssertClose(item.TargetCarbs, suggestion.Carbs);
        AssertClose(item.TargetFats, suggestion.Fats);
        AssertClose(suggestion.Calories, suggestion.Components.Sum(component => ForPortion(component.Calories, component.Grams)));
        AssertClose(suggestion.Protein, suggestion.Components.Sum(component => ForPortion(component.Protein, component.Grams)));
        AssertClose(suggestion.Carbs, suggestion.Components.Sum(component => ForPortion(component.Carbs, component.Grams)));
        AssertClose(suggestion.Fats, suggestion.Components.Sum(component => ForPortion(component.Fats, component.Grams)));
        Assert.All(suggestion.Components, component =>
        {
            Assert.True(float.IsFinite(component.Grams));
            Assert.Equal(0, component.FoodItemId);
            Assert.True(component.Grams > 0f && component.Calories >= 0f && component.Protein >= 0f &&
                component.Carbs >= 0f && component.Fats >= 0f);
        });
        Assert.InRange(suggestion.Components.Where(IsOil).Sum(component => component.Grams),
            0f, PortionCalculationRule.MaxOilGrams);
        Assert.All(suggestion.Components.Where(IsOil), oil =>
        {
            Assert.Equal(0f, oil.Protein);
            Assert.Equal(0f, oil.Carbs);
            AssertClose(oil.Grams, ForPortion(oil.Fats, oil.Grams));
            AssertClose(oil.Grams * GeneratedOilPortionRule.CaloriesPerGram, ForPortion(oil.Calories, oil.Grams));
        });
    }

    private static float ForPortion(float density, float grams) => density * grams / NutrientReferenceGrams;

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(actual, expected - Tolerance, expected + Tolerance);

    private sealed class SyntheticTargetStore(float calories = DailyCalories) : IServerNutritionTargetProjectionStore
    {
        public Guid LastUserId { get; private set; }

        public Task<ServerNutritionTargetProjection> LoadAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUserId = userId;
            return Task.FromResult(new ServerNutritionTargetProjection(calories, DailyProtein, DailyCarbs,
                DailyFats, "Synthetic", "Synthetic", null, null, null, null, null, false, false,
                CyclePhase.Unknown, "Unknown", "Synthetic"));
        }
    }
}
