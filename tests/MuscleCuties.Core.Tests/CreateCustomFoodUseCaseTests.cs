using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CreateCustomFoodUseCaseTests
{
    private const float ServingAmount = 100f;
    private const string ServingUnitGrams = "g";
    private const float TestCalories = 250f;
    private const float TestProtein = 20f;
    private const float TestCarbs = 30f;
    private const float TestFats = 5f;

    [Fact]
    public async Task CreateCustomFood_SucceedsWithBlankCompany_AndNormalizedMicronutrients()
    {
        var mockStore = new InMemoryCustomFoodStore();
        var useCase = new CreateCustomFoodUseCase(mockStore);

        var request = new CreateCustomFoodRequest(
            Name: "Organic Rolled Oats",
            ServingAmount: ServingAmount,
            ServingUnit: ServingUnitGrams,
            Calories: TestCalories,
            Protein: TestProtein,
            Carbs: TestCarbs,
            Fats: TestFats,
            CompanySource: "   ", // Blank company should be accepted
            IronMilligrams: 3.5f,
            VitaminB12Micrograms: 1.2f,
            VitaminDMicrograms: null,
            CalciumMilligrams: 45f);

        var userId = Guid.NewGuid();
        var result = await useCase.ExecuteAsync(request, userId);

        Assert.NotNull(result);
        Assert.Equal("Organic Rolled Oats", result.Name);
        Assert.Null(result.CompanySource); // Trimmed blank converted to null
        Assert.NotNull(mockStore.LastCreatedRecord);
        Assert.Null(mockStore.LastCreatedRecord.CompanySource);
        Assert.Equal(3.5f, mockStore.LastCreatedRecord.IronMilligrams);
        Assert.Equal(1.2f, mockStore.LastCreatedRecord.VitaminB12Micrograms);
        Assert.Null(mockStore.LastCreatedRecord.VitaminDMicrograms);
        Assert.Equal(45f, mockStore.LastCreatedRecord.CalciumMilligrams);
    }

    private sealed class InMemoryCustomFoodStore : ICreateCustomFoodStore
    {
        public CustomFoodRecord? LastCreatedRecord { get; private set; }

        public Task<CustomFoodRecord> CreateAsync(
            Guid ownerUserId,
            CustomFoodNutrition nutrition,
            CustomFoodMicronutrients micronutrients = default,
            string? companySource = null,
            CancellationToken cancellationToken = default)
        {
            var brandOwner = string.IsNullOrWhiteSpace(companySource) ? null : companySource.Trim();
            var record = new CustomFoodRecord(
                Id: Guid.NewGuid(),
                Name: nutrition.Name,
                Calories: nutrition.CaloriesPer100Grams,
                Protein: nutrition.ProteinPer100Grams,
                Carbs: nutrition.CarbsPer100Grams,
                Fats: nutrition.FatsPer100Grams,
                ServingSize: nutrition.ServingAmount,
                ServingSizeUnit: nutrition.ServingUnit,
                CompanySource: brandOwner,
                IronMilligrams: (float?)micronutrients.IronMilligramsPer100Grams,
                VitaminB12Micrograms: (float?)micronutrients.VitaminB12MicrogramsPer100Grams,
                VitaminDMicrograms: (float?)micronutrients.VitaminDMicrogramsPer100Grams,
                CalciumMilligrams: (float?)micronutrients.CalciumMilligramsPer100Grams);

            LastCreatedRecord = record;
            return Task.FromResult(record);
        }
    }
}
