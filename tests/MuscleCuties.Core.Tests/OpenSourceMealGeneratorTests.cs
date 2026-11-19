using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class OpenSourceMealGeneratorTests
{
    private const float TargetCalories = 550f;
    private const float TargetFats = 18f;
    private const float Tolerance = 0.05f;
    private const float NutrientReferenceGrams = 100f;

    [Fact]
    public async Task GenerateMealAsync_UsesExternalUsdaNutrientsAndMatchesTargetCalories()
    {
        var source = new StubExternalFoodSource(new Dictionary<string, ExternalFoodItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["chicken breast"] = new(171077, "Chicken, broilers or fryers, breast, meat only, cooked, roasted", 165f, 31.02f, 0f, 3.57f, "SR Legacy"),
            ["quinoa"] = new(168917, "Quinoa, cooked", 120f, 4.4f, 21.3f, 1.92f, "SR Legacy"),
            ["broccoli"] = new(170379, "Broccoli, cooked, boiled, drained", 35f, 2.38f, 7.18f, 0.41f, "SR Legacy"),
            ["olive oil"] = new(171413, "Oil, olive, salad or cooking", 884f, 0f, 0f, 100f, "SR Legacy")
        });

        var generator = new OpenSourceMealGenerator(source);
        var suggestion = await generator.GenerateMealAsync(
            "Lunch",
            "Savoury",
            TargetCalories,
            TargetFats,
            excludeConceptName: null,
            seedIndex: 0);

        Assert.NotNull(suggestion);
        Assert.Equal("USDA FoodData Central", suggestion!.Style);
        Assert.NotEmpty(suggestion.Components);
        Assert.InRange(suggestion.Calories, TargetCalories - Tolerance, TargetCalories + Tolerance);
        var componentCalories = suggestion.Components.Sum(c => c.Calories * c.Grams / NutrientReferenceGrams);
        Assert.InRange(componentCalories, suggestion.Calories - Tolerance, suggestion.Calories + Tolerance);
        var oilGrams = suggestion.Components
            .Where(c => c.Name.EndsWith(" Oil", StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Grams);
        Assert.InRange(oilGrams, 0f, PortionCalculationRule.MaxOilGrams);
        Assert.True(source.CallCount > 0);
    }

    [Fact]
    public async Task GenerateMealAsync_CyclesDistinctConceptsAcrossRefreshAndCachesQueries()
    {
        var source = new StubExternalFoodSource(new Dictionary<string, ExternalFoodItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["chicken breast"] = new(171077, "Chicken breast, roasted", 165f, 31f, 0f, 3.6f, "SR Legacy"),
            ["quinoa"] = new(168917, "Quinoa, cooked", 120f, 4.4f, 21.3f, 1.9f, "SR Legacy"),
            ["broccoli"] = new(170379, "Broccoli, cooked", 35f, 2.4f, 7.2f, 0.4f, "SR Legacy"),
            ["olive oil"] = new(171413, "Olive oil", 884f, 0f, 0f, 100f, "SR Legacy"),
            ["tuna"] = new(175159, "Fish, tuna, light, canned in water", 116f, 25.5f, 0f, 0.8f, "SR Legacy"),
            ["brown rice"] = new(169704, "Rice, brown, long-grain, cooked", 123f, 2.7f, 25.6f, 1f, "SR Legacy"),
            ["cucumber"] = new(168409, "Cucumber, with peel, raw", 15f, 0.7f, 3.6f, 0.1f, "SR Legacy"),
            ["avocado"] = new(171705, "Avocados, raw", 160f, 2f, 8.5f, 14.7f, "SR Legacy"),
            ["lentils"] = new(172421, "Lentils, mature seeds, cooked", 116f, 9f, 20.1f, 0.4f, "SR Legacy"),
            ["sweet potato"] = new(168483, "Sweet potato, cooked, baked", 90f, 2f, 20.7f, 0.2f, "SR Legacy"),
            ["spinach"] = new(168462, "Spinach, raw", 23f, 2.9f, 3.6f, 0.4f, "SR Legacy"),
            ["tahini"] = new(170189, "Seeds, sesame butter, tahini", 595f, 17f, 21f, 54f, "SR Legacy")
        });

        var generator = new OpenSourceMealGenerator(source);
        var first = await generator.GenerateMealAsync("Lunch", "Savoury", TargetCalories, TargetFats, null, seedIndex: 0);
        var second = await generator.GenerateMealAsync("Lunch", "Savoury", TargetCalories, TargetFats, first!.ConceptName);
        var third = await generator.GenerateMealAsync("Lunch", "Savoury", TargetCalories, TargetFats, second!.ConceptName);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);
        Assert.NotEqual(first!.ConceptName, second!.ConceptName);
        Assert.NotEqual(second.ConceptName, third!.ConceptName);
        Assert.NotEqual(first.ConceptName, third.ConceptName);

        var callsBeforeRepeat = source.CallCount;
        var repeat = await generator.GenerateMealAsync("Lunch", "Savoury", TargetCalories, TargetFats, null, seedIndex: 0);
        Assert.Equal(first.ConceptName, repeat!.ConceptName);
        Assert.Equal(callsBeforeRepeat, source.CallCount);
    }

    [Fact]
    public async Task GenerateMealAsync_ReturnsNullWhenExternalSourceIsUnconfigured()
    {
        var source = new StubExternalFoodSource(new Dictionary<string, ExternalFoodItem>(), isConfigured: false);
        var generator = new OpenSourceMealGenerator(source);

        var suggestion = await generator.GenerateMealAsync("Dinner", "Savoury", TargetCalories, TargetFats);

        Assert.Null(suggestion);
        Assert.Equal(0, source.CallCount);
    }

    private sealed class StubExternalFoodSource(
        IReadOnlyDictionary<string, ExternalFoodItem> itemsByQuery,
        bool isConfigured = true) : IExternalFoodSource
    {
        public bool IsConfigured => isConfigured;
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<ExternalFoodItem>> SearchAsync(
            string genericQuery,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (itemsByQuery.TryGetValue(genericQuery, out var item))
                return Task.FromResult<IReadOnlyList<ExternalFoodItem>>([item]);
            return Task.FromResult<IReadOnlyList<ExternalFoodItem>>([]);
        }
    }
}
