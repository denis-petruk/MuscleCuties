using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class ExternalFoodFallbackSearchStoreTests
{
    private static FoodCatalogCandidate CreateCandidate(
        int id,
        string name,
        float? calories,
        float? protein,
        float? carbs,
        float? fats,
        bool isCustom,
        int? fdcId = null,
        string? dataType = null) =>
        new(id, name, calories, protein, carbs, fats, isCustom, fdcId, dataType, null, null, null, null, 100f, "g", null);

    [Fact]
    public void FoodCatalogProvenanceRule_MarksFdcItemsAsUsdaFoodDataCentral()
    {
        var fdcCandidate = CreateCandidate(
            12345,
            "Oats, rolled, dry",
            389f,
            16.89f,
            66.27f,
            6.9f,
            isCustom: false,
            fdcId: 173904);

        var customCandidate = CreateCandidate(
            1,
            "Custom Shake",
            200f,
            30f,
            10f,
            5f,
            isCustom: true,
            fdcId: null);

        var localCandidate = CreateCandidate(
            2,
            "Local Apple",
            95f,
            0.5f,
            25f,
            0.3f,
            isCustom: false,
            fdcId: null);

        Assert.Equal(FoodCatalogSourceKind.UsdaFoodDataCentral, FoodCatalogProvenanceRule.Classify(fdcCandidate));
        Assert.Equal(FoodCatalogSourceKind.CustomFood, FoodCatalogProvenanceRule.Classify(customCandidate));
        Assert.Equal(FoodCatalogSourceKind.LocalFood, FoodCatalogProvenanceRule.Classify(localCandidate));
    }

    [Fact]
    public async Task SearchCandidatesAsync_DoesNotCallExternalSource_WhenCandidatesMeetThreshold()
    {
        var candidates = Enumerable.Range(1, 5)
            .Select(i => CreateCandidate(i, $"Food {i}", 100f, 10f, 10f, 2f, false))
            .ToList();

        var innerStore = new StubSearchStore(candidates);
        var source = new StubExternalFoodSource(isConfigured: true, items: []);
        var importStore = new StubImportStore();

        var store = new ExternalFoodFallbackSearchStore(innerStore, source, importStore);
        var result = await store.SearchCandidatesAsync("oats", Guid.NewGuid());

        Assert.Equal(5, result.Count);
        Assert.Equal(0, source.SearchCallCount);
        Assert.Equal(0, importStore.ImportCallCount);
    }

    [Fact]
    public async Task SearchCandidatesAsync_DoesNotCallExternalSource_WhenSourceIsNotConfigured()
    {
        var candidates = new List<FoodCatalogCandidate>
        {
            CreateCandidate(1, "Food 1", 100f, 10f, 10f, 2f, false)
        };

        var innerStore = new StubSearchStore(candidates);
        var source = new StubExternalFoodSource(isConfigured: false, items: []);
        var importStore = new StubImportStore();

        var store = new ExternalFoodFallbackSearchStore(innerStore, source, importStore);
        var result = await store.SearchCandidatesAsync("oats", Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal(0, source.SearchCallCount);
    }

    [Fact]
    public async Task SearchCandidatesAsync_FetchesAndImports_WhenSparseAndConfigured()
    {
        var candidates = new List<FoodCatalogCandidate>
        {
            CreateCandidate(1, "Local Oats", 350f, 12f, 60f, 5f, false, fdcId: 100)
        };

        var externalItems = new List<ExternalFoodItem>
        {
            new(100, "Local Oats (duplicate FDC)", 350f, 12f, 60f, 5f, "Foundation"),
            new(200, "Rolled Oats", 379f, 13f, 67f, 6.5f, "Foundation"),
            new(300, "Steel Cut Oats", 375f, 12.5f, 68f, 6f, "SR Legacy")
        };

        var ownerId = Guid.NewGuid();
        var innerStore = new StubSearchStore(candidates);
        var source = new StubExternalFoodSource(isConfigured: true, items: externalItems);
        var importStore = new StubImportStore();

        var store = new ExternalFoodFallbackSearchStore(innerStore, source, importStore);
        var result = await store.SearchCandidatesAsync("oats", ownerId);

        Assert.Equal(1, source.SearchCallCount);
        Assert.Equal(1, importStore.ImportCallCount);
        // Only 2 fresh items should be imported (item 100 was skipped as already known)
        Assert.Equal(2, importStore.LastImportedItems.Count);
        Assert.Equal(3, result.Count);
    }

    private sealed class StubSearchStore(List<FoodCatalogCandidate> candidates) : IFoodCatalogSearchStore
    {
        public Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
            string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FoodCatalogCandidate>>(candidates);

        public Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
            string query, Guid? ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FoodCatalogCandidate>>(candidates);
    }

    private sealed class StubExternalFoodSource(bool isConfigured, IReadOnlyList<ExternalFoodItem> items) : IExternalFoodSource
    {
        public bool IsConfigured => isConfigured;
        public int SearchCallCount { get; private set; }

        public Task<IReadOnlyList<ExternalFoodItem>> SearchAsync(string genericQuery, CancellationToken cancellationToken = default)
        {
            SearchCallCount++;
            return Task.FromResult(items);
        }
    }

    private sealed class StubImportStore : IExternalFoodImportStore
    {
        public int ImportCallCount { get; private set; }
        public IReadOnlyList<ExternalFoodItem> LastImportedItems { get; private set; } = [];

        public Task<IReadOnlyList<FoodCatalogCandidate>> ImportAsync(
            Guid ownerUserId,
            IReadOnlyList<ExternalFoodItem> items,
            CancellationToken cancellationToken = default)
        {
            ImportCallCount++;
            LastImportedItems = items;
            var mapped = items.Select(item => CreateCandidate(
                item.FdcId,
                item.Name,
                item.CaloriesPer100Grams,
                item.ProteinPer100Grams,
                item.CarbsPer100Grams,
                item.FatsPer100Grams,
                isCustom: false,
                fdcId: item.FdcId,
                dataType: item.DataType)).ToList();
            return Task.FromResult<IReadOnlyList<FoodCatalogCandidate>>(mapped);
        }
    }
}
