using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class RecipePortionTests
{
    private const float BatchServings = 4f;
    private const float RawGrams = 800f;
    private const float CookedGrams = 600f;
    private const float BatchCalories = 1200f;
    private const float BatchProtein = 80f;
    private const float BatchCarbs = 120f;
    private const float BatchNetCarbs = 100f;
    private const float BatchFats = 40f;
    private const float HalfServing = 0.5f;
    private const float TwoServings = 2f;
    private const float ChosenGrams = 150f;
    private const float HalfServingCookedGrams = 75f;
    private const float TwoServingsCookedGrams = 300f;
    private const float HalfServingCalories = 150f;
    private const float TwoServingsCalories = 600f;
    private const float HalfServingProtein = 10f;
    private const float TwoServingsProtein = 40f;
    private const int ExpectedPreviewReadCount = 2;
    private const int NoWrites = 0;
    private const float InvalidZeroAmount = 0f;
    private const float InvalidNegativeAmount = -1f;
    private static readonly Guid RecipeId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static RecipeBatchTotals Batch(float? cookedGrams = CookedGrams) => new(
        BatchServings, RawGrams, cookedGrams, BatchCalories, BatchProtein, BatchCarbs,
        BatchNetCarbs, BatchFats);

    [Theory]
    [InlineData(HalfServing, HalfServingCookedGrams, HalfServingCalories, HalfServingProtein)]
    [InlineData(TwoServings, TwoServingsCookedGrams, TwoServingsCalories, TwoServingsProtein)]
    public void ServingPreviewScalesFromSavedBatch(float servings, float grams, float calories, float protein)
    {
        var portion = RecipeBatchCalculationRule.CalculatePortion(Batch(), servings, null);

        Assert.Equal(servings, portion.SelectedServingsCount);
        Assert.Equal(grams, portion.SelectedGrams);
        Assert.Equal(calories, portion.Calories);
        Assert.Equal(protein, portion.Protein);
        Assert.Equal(BatchCalories, portion.Batch.TotalCalories);
        Assert.Equal(CookedGrams, portion.Batch.CookedWeightGrams);
    }

    [Fact]
    public void GramsPreviewUsesCookedWeightAndRawWeightFallback()
    {
        var cookedPortion = RecipeBatchCalculationRule.CalculatePortion(Batch(), null, ChosenGrams);
        var rawPortion = RecipeBatchCalculationRule.CalculatePortion(Batch(null), null, ChosenGrams);

        Assert.Equal(BatchCalories / BatchServings, cookedPortion.Calories);
        Assert.Equal(ChosenGrams * BatchCalories / RawGrams, rawPortion.Calories);
        Assert.Null(rawPortion.Batch.CookedWeightGrams);
    }

    [Theory]
    [InlineData(InvalidZeroAmount)]
    [InlineData(InvalidNegativeAmount)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidSelectionsAreRejected(float amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecipeBatchCalculationRule.CalculatePortion(Batch(), amount, null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecipeBatchCalculationRule.CalculatePortion(Batch(), null, amount));
    }

    [Fact]
    public void AmbiguousSelectionAndOverflowAreRejected()
    {
        Assert.Throws<ArgumentException>(() => RecipeBatchCalculationRule.CalculatePortion(Batch(), null, null));
        Assert.Throws<ArgumentException>(() =>
            RecipeBatchCalculationRule.CalculatePortion(Batch(), HalfServing, ChosenGrams));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecipeBatchCalculationRule.CalculatePortion(Batch() with { TotalCalories = float.MaxValue },
                float.MaxValue, null));
    }

    [Fact]
    public void InvalidSavedBatchIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecipeBatchCalculationRule.CalculatePortion(Batch() with { CookedWeightGrams = float.NaN },
                HalfServing, null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecipeBatchCalculationRule.CalculatePortion(Batch() with { TotalProtein = float.PositiveInfinity },
                HalfServing, null));
    }

    [Fact]
    public async Task PreviewReadsTenantRecipeWithoutWriting()
    {
        var store = new FakeRecipeStore { Recipe = SavedRecipe() };
        var useCase = new PreviewRecipePortionUseCase(store);

        var half = await useCase.ExecuteAsync(UserId, new PreviewRecipePortionRequest(RecipeId, HalfServing, null));
        var two = await useCase.ExecuteAsync(UserId, new PreviewRecipePortionRequest(RecipeId, TwoServings, null));

        Assert.Equal(ExpectedPreviewReadCount, store.ReadCount);
        Assert.Equal(NoWrites, store.WriteCount);
        Assert.Equal(UserId, store.LastUserId);
        Assert.Equal(HalfServingCalories, half.Calories);
        Assert.Equal(TwoServingsCalories, two.Calories);
        Assert.Equal(BatchCalories, two.BatchCalories);
        Assert.Equal(CookedGrams, two.BatchCookedWeightGrams);
    }

    [Fact]
    public async Task PreviewRejectsUnknownTenantRecipe()
    {
        var store = new FakeRecipeStore();
        var useCase = new PreviewRecipePortionUseCase(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            useCase.ExecuteAsync(UserId, new PreviewRecipePortionRequest(RecipeId, HalfServing, null)));
        Assert.Equal(NoWrites, store.WriteCount);
    }

    [Fact]
    public async Task TenantPreviewFailsClosedWhenStoreOnlyImplementsUnscopedReads()
    {
        var store = new UnscopedOnlyRecipeStore();
        var useCase = new PreviewRecipePortionUseCase(store);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            useCase.ExecuteAsync(UserId, new PreviewRecipePortionRequest(RecipeId, HalfServing, null)));
        Assert.Equal(NoWrites, store.UnscopedReadCount);
    }

    private static RecipeDetailsDto SavedRecipe() => new(RecipeId, "Soup", null,
        BatchServings, CookedGrams, RawGrams, BatchCalories, BatchProtein, BatchCarbs,
        BatchNetCarbs, BatchFats, BatchCalories / BatchServings,
        BatchProtein / BatchServings, BatchCarbs / BatchServings,
        BatchNetCarbs / BatchServings, BatchFats / BatchServings,
        CookedGrams / BatchServings, [], DateTime.UnixEpoch);

    private sealed class FakeRecipeStore : IRecipeStore
    {
        public RecipeDetailsDto? Recipe { get; init; }
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public Guid LastUserId { get; private set; }

        public Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid userId, Guid id,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            ReadCount++;
            return Task.FromResult(id == RecipeId ? Recipe : null);
        }

        public Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Preview must use a tenant-scoped read.");

        public Task<RecipeDetailsDto> SaveRecipeAsync(RecipeDetailsDto recipe,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            throw new InvalidOperationException("Preview must not write.");
        }

        public Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<MealTemplateDto> SaveMealTemplateAsync(MealTemplateDto template,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(int? mealType = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteMealTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnscopedOnlyRecipeStore : IRecipeStore
    {
        public int UnscopedReadCount { get; private set; }

        public Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            UnscopedReadCount++;
            return Task.FromResult<RecipeDetailsDto?>(SavedRecipe());
        }

        public Task<RecipeDetailsDto> SaveRecipeAsync(RecipeDetailsDto recipe,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<MealTemplateDto> SaveMealTemplateAsync(MealTemplateDto template,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(int? mealType = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteMealTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
