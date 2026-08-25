using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class FoodSyncServiceTests : IDisposable
{
    private readonly DatabaseFixture _fixture = new();
    private readonly NutritionRepository _nutritionRepository;
    private readonly FoodSyncRepository _foodSyncRepository;
    private readonly FakeFdcApiClient _fdcApiClient = new();
    private readonly FoodSyncService _service;

    public FoodSyncServiceTests()
    {
        _nutritionRepository = new NutritionRepository(_fixture.Db);
        _foodSyncRepository = new FoodSyncRepository(_fixture.Db);
        _service = new FoodSyncService(_nutritionRepository, _foodSyncRepository, _fdcApiClient);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task SearchAsync_WhenQueryIsBlank_DoesNotCallRemoteOrLog()
    {
        var results = await _service.SearchAsync(" ");

        Assert.Empty(results);
        Assert.Equal(0, _fdcApiClient.SearchCallCount);
        Assert.Null(await _foodSyncRepository.GetLatestSyncLogAsync());
    }

    [Fact]
    public async Task SearchAsync_WhenLocalHasResults_StillRefreshesRemoteCandidates()
    {
        for (var i = 1; i <= 5; i++)
        {
            await _nutritionRepository.AddAsync(new FoodItem
            {
                Name = $"Rolled oats {i}",
                Calories = 100 + i,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var results = await _service.SearchAsync("oats");

        Assert.Equal(5, results.Count);
        Assert.Equal(1, _fdcApiClient.SearchCallCount);
        Assert.Equal(15, _fdcApiClient.LastSearchPageSize);
        Assert.Equal(1, _fdcApiClient.LastSearchPageNumber);
        Assert.Equal(0, _fdcApiClient.BatchCallCount);

        var log = await _foodSyncRepository.GetLatestSyncLogAsync();
        Assert.NotNull(log);
        Assert.Equal("Success", log.Status);
        Assert.Equal(0, log.ItemsUpserted);
    }

    [Fact]
    public async Task SearchAsync_WhenLocalResultsAreDuplicates_StillCallsRemote()
    {
        for (var i = 1; i <= 5; i++)
        {
            await _nutritionRepository.AddAsync(new FoodItem
            {
                Name = i % 2 == 0 ? "CHICKEN" : "Chicken",
                Calories = 120 + i,
                Protein = 22,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _fdcApiClient.SearchResults.Add(new FdcFoodSearchResult
        {
            FdcId = 331960,
            Description = "Chicken breast, raw",
            DataType = "Foundation"
        });
        _fdcApiClient.Foods[331960] = BuildDetail(331960, "Chicken breast, raw", (1008, 120f), (1003, 22.5f));

        var results = await _service.SearchAsync("chicken");

        Assert.Equal(1, _fdcApiClient.SearchCallCount);
        Assert.Contains(results, food => food.Name == "Chicken breast, raw");
    }

    [Fact]
    public async Task SearchAsync_WhenLocalIsSparse_UpsertsRemoteDetailsAndLogsSuccess()
    {
        _fdcApiClient.SearchResults.Add(new FdcFoodSearchResult
        {
            FdcId = 173904,
            Description = "Oats, rolled, dry"
        });
        _fdcApiClient.Foods[173904] = BuildDetail(173904, "Oats, rolled, dry", (1008, 389f), (1003, 16.9f));

        var results = await _service.SearchAsync("oats");

        var saved = await _nutritionRepository.GetFoodItemByFdcIdAsync(173904);
        Assert.NotNull(saved);
        Assert.Equal("Oats, rolled, dry", saved.Name);
        Assert.Equal(389f, saved.Calories);
        Assert.Equal(16.9f, saved.Protein);
        Assert.Contains(results, f => f.FdcId == 173904);
        Assert.Equal(1, _fdcApiClient.SearchCallCount);
        Assert.Equal(1, _fdcApiClient.BatchCallCount);

        var log = await _foodSyncRepository.GetLatestSyncLogAsync();
        Assert.NotNull(log);
        Assert.Equal("Success", log.Status);
        Assert.Equal(1, log.ItemsUpserted);
        Assert.Equal(0, log.ItemsFailed);
        Assert.NotNull(log.CompletedAt);
    }

    [Fact]
    public async Task SearchAsync_WhenRemoteReturnsManyMatches_UsesSearchPayloadWithoutFetchingAllDetails()
    {
        for (var i = 1; i <= 45; i++)
        {
            var fdcId = 900000 + i;
            _fdcApiClient.SearchResults.Add(new FdcFoodSearchResult
            {
                FdcId = fdcId,
                Description = $"Batch protein {i}",
                DataType = "Foundation",
                FoodNutrients =
                [
                    new FdcSearchNutrient { NutrientId = 1008, Value = 100f + i },
                    new FdcSearchNutrient { NutrientId = 1003, Value = 20f },
                    new FdcSearchNutrient { NutrientId = 1005, Value = 10f }
                ]
            });
        }

        var results = await _service.SearchAsync("batch protein");

        Assert.Equal(15, results.Count);
        Assert.Equal(1, _fdcApiClient.SearchCallCount);
        Assert.Equal(15, _fdcApiClient.LastSearchPageSize);
        Assert.Equal(1, _fdcApiClient.LastSearchPageNumber);
        Assert.Equal(0, _fdcApiClient.BatchCallCount);
        Assert.Empty(_fdcApiClient.BatchSizes);
    }

    [Fact]
    public async Task SearchAsync_WhenPageTwoRequested_CallsRemoteSecondPage()
    {
        for (var i = 1; i <= 30; i++)
        {
            var fdcId = 920000 + i;
            _fdcApiClient.SearchResults.Add(new FdcFoodSearchResult
            {
                FdcId = fdcId,
                Description = $"Paged food {i}",
                DataType = "Foundation",
                FoodNutrients =
                [
                    new FdcSearchNutrient { NutrientId = 1008, Value = 100f + i },
                    new FdcSearchNutrient { NutrientId = 1003, Value = 20f },
                    new FdcSearchNutrient { NutrientId = 1005, Value = 10f }
                ]
            });
        }

        var results = await _service.SearchAsync("paged food", pageSize: 15, pageNumber: 2);

        Assert.Equal(15, results.Count);
        Assert.Equal(2, _fdcApiClient.LastSearchPageNumber);
        Assert.Contains(results, food => food.Name == "Paged food 16");
        Assert.DoesNotContain(results, food => food.Name == "Paged food 1");
    }

    [Fact]
    public async Task SearchAsync_WhenRemoteNutritionIsMissing_RefreshesOnlyTopDetails()
    {
        for (var i = 1; i <= 20; i++)
        {
            var fdcId = 910000 + i;
            _fdcApiClient.SearchResults.Add(new FdcFoodSearchResult
            {
                FdcId = fdcId,
                Description = $"Missing nutrition {i}",
                DataType = "Foundation"
            });
            _fdcApiClient.Foods[fdcId] = BuildDetail(
                fdcId,
                $"Missing nutrition {i}",
                (1008, 100f + i),
                (1003, 20f));
        }

        var results = await _service.SearchAsync("missing nutrition");

        Assert.Equal(8, results.Count);
        Assert.Equal(1, _fdcApiClient.SearchCallCount);
        Assert.Equal(1, _fdcApiClient.BatchCallCount);
        Assert.Equal([8], _fdcApiClient.BatchSizes);
    }

    [Fact]
    public async Task SearchAsync_BrandedFood_PreservesSearchMetadataForChoosingRightVersion()
    {
        _fdcApiClient.SearchResults.Add(new FdcFoodSearchResult
        {
            FdcId = 700001,
            Description = "Protein bar",
            DataType = "Branded",
            BrandOwner = "Acme Nutrition",
            BrandName = "Acme",
            GtinUpc = "123456789",
            ServingSize = 50f,
            ServingSizeUnit = "g",
            FoodNutrients =
            [
                new FdcSearchNutrient { NutrientId = 1008, Value = 220f },
                new FdcSearchNutrient { NutrientId = 1003, Value = 20f }
            ]
        });
        _fdcApiClient.Foods[700001] = new FdcFoodDetail
        {
            FdcId = 700001,
            Description = "Protein bar",
            FoodNutrients =
            [
                new FdcFoodDetailNutrient { NutrientId = 1008, Value = 220f }
            ]
        };

        var results = await _service.SearchAsync("acme protein");

        var result = Assert.Single(results);
        Assert.Equal("Branded", result.DataType);
        Assert.Equal("Acme Nutrition", result.BrandOwner);
        Assert.Equal("Acme", result.BrandName);
        Assert.Equal("123456789", result.GtinUpc);
        Assert.Equal(50f, result.ServingSize);
        Assert.Equal("g", result.ServingSizeUnit);
        Assert.Equal(20f, result.Protein);

        var saved = await _nutritionRepository.GetFoodItemByFdcIdAsync(700001);
        Assert.NotNull(saved);
        Assert.Equal("Acme Nutrition", saved.BrandOwner);
    }

    [Fact]
    public async Task SearchAsync_WhenDetailBatchOmitsItem_UsesSearchResultNutritionFallback()
    {
        _fdcApiClient.SearchResults.Add(new FdcFoodSearchResult
        {
            FdcId = 700002,
            Description = "Oats cereal",
            DataType = "Branded",
            BrandOwner = "Acme Foods",
            FoodNutrients =
            [
                new FdcSearchNutrient { NutrientId = 1008, Value = 389f },
                new FdcSearchNutrient { NutrientId = 1003, Value = 16.9f },
                new FdcSearchNutrient { NutrientId = 1005, Value = 66.3f },
                new FdcSearchNutrient { NutrientId = 1004, Value = 6.9f }
            ]
        });

        var results = await _service.SearchAsync("acme oats");

        var result = Assert.Single(results);
        Assert.Equal(389f, result.Calories);
        Assert.Equal(16.9f, result.Protein);
        Assert.Equal(66.3f, result.Carbs);
        Assert.Equal(6.9f, result.Fats);
        Assert.Equal(0, _fdcApiClient.BatchCallCount);
    }

    [Fact]
    public async Task FetchDetailAsync_WhenExistingFoodChanges_WritesVersionBeforeUpdating()
    {
        await _nutritionRepository.AddAsync(new FoodItem
        {
            Name = "Oats, old values",
            Calories = 100,
            Protein = 4,
            FdcId = 173904,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow.AddDays(-7)
        });

        _fdcApiClient.Foods[173904] = BuildDetail(173904, "Oats, rolled, dry", (1008, 389f), (1003, 16.9f));

        var result = await _service.FetchDetailAsync(173904);

        Assert.NotNull(result);
        Assert.Equal(389f, result.Calories);
        Assert.Equal(16.9f, result.Protein);

        var version = await _fixture.Db.FoodItemVersions.SingleAsync();
        Assert.Equal(result.Id, version.FoodItemId);
        Assert.Equal("FDC", version.ChangeSource);
        Assert.Contains("\"Calories\":100", version.NutrientJson);
    }

    private static FdcFoodDetail BuildDetail(int fdcId, string name, params (int Id, float Amount)[] nutrients) =>
        new()
        {
            FdcId = fdcId,
            Description = name,
            DataType = "Foundation",
            FoodNutrients = nutrients
                .Select(n => new FdcFoodDetailNutrient
                {
                    Nutrient = new FdcNutrient { Id = n.Id },
                    Amount = n.Amount
                })
                .ToList()
        };

    private sealed class FakeFdcApiClient : IFdcApiClient
    {
        public int SearchCallCount { get; private set; }
        public int BatchCallCount { get; private set; }
        public int LastSearchPageSize { get; private set; }
        public int LastSearchPageNumber { get; private set; }
        public List<int> BatchSizes { get; } = [];
        public List<FdcFoodSearchResult> SearchResults { get; } = [];
        public Dictionary<int, FdcFoodDetail> Foods { get; } = [];

        public Task<IReadOnlyList<FdcFoodSearchResult>> SearchFoodsAsync(
            string query,
            int pageSize = 20,
            int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            SearchCallCount++;
            LastSearchPageSize = pageSize;
            LastSearchPageNumber = pageNumber;

            var page = SearchResults
                .Skip((Math.Max(1, pageNumber) - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<IReadOnlyList<FdcFoodSearchResult>>(page);
        }

        public Task<FdcFoodDetail?> GetFoodAsync(int fdcId, CancellationToken cancellationToken = default)
        {
            Foods.TryGetValue(fdcId, out var food);
            return Task.FromResult(food);
        }

        public Task<IReadOnlyList<FdcFoodDetail>> GetFoodsAsync(
            IEnumerable<int> fdcIds,
            CancellationToken cancellationToken = default)
        {
            BatchCallCount++;
            var ids = fdcIds.ToList();
            BatchSizes.Add(ids.Count);

            var foods = ids
                .Where(Foods.ContainsKey)
                .Select(id => Foods[id])
                .ToList();

            return Task.FromResult<IReadOnlyList<FdcFoodDetail>>(foods);
        }
    }
}
