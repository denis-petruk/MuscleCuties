using System.Text.Json;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using Diary = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.Registration.Tests;

public sealed class CloudNutritionContractTests
{
    private static readonly DateOnly SourceDate = new(2026, 10, 19);
    private static readonly DateOnly TargetDate = new(2026, 10, 20);
    private static readonly Guid ItemId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid GroupedMealId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly DateTime LoggedAtUtc = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Xunit.Fact]
    public async Task BatchRetryUsesSameKeyAndEditedValuesUseNewKey()
    {
        var api = new RecordingApiClient();
        var store = new CloudNutritionDailyLogStore(api);
        var original = Draft(120);

        await store.BatchSaveFoodItemsAsync(SourceDate, [original]);
        await store.BatchSaveFoodItemsAsync(SourceDate, [original]);
        await store.BatchSaveFoodItemsAsync(SourceDate, [original with { Calories = 125 }]);

        var calls = api.Calls.Where(call => call.Path == "/api/v1/nutrition/diary/batch").ToArray();
        Xunit.Assert.Equal(3, calls.Length);
        Xunit.Assert.All(calls, call => Xunit.Assert.Equal("POST", call.Method));
        Xunit.Assert.Equal(calls[0].IdempotencyKey, calls[1].IdempotencyKey);
        Xunit.Assert.NotEqual(calls[0].IdempotencyKey, calls[2].IdempotencyKey);
        var first = Xunit.Assert.IsType<Diary.BatchSaveNutritionDiaryEntries>(calls[0].Payload);
        Xunit.Assert.Equal(SourceDate, first.Date);
        Xunit.Assert.Null(Xunit.Assert.Single(first.Entries).Identity);
        Xunit.Assert.Equal(SourceDate, Xunit.Assert.Single(first.Entries).Date);
    }

    [Xunit.Fact]
    public async Task CopyKeyTracksSourceEntriesAndPreservesSelectedDates()
    {
        var api = new RecordingApiClient { Day = Day([Entry("snapshot:" + ItemId.ToString("N"), 120)]) };
        var store = new CloudNutritionDailyLogStore(api);

        await store.CopyEntriesAsync(SourceDate, TargetDate, MealType.Breakfast);
        await store.CopyEntriesAsync(SourceDate, TargetDate, MealType.Breakfast);
        api.Day = Day([Entry("snapshot:" + ItemId.ToString("N"), 125)]);
        await store.CopyEntriesAsync(SourceDate, TargetDate, MealType.Breakfast);

        var calls = api.Calls.Where(call => call.Path == "/api/v1/nutrition/diary/copy").ToArray();
        Xunit.Assert.Equal(3, calls.Length);
        Xunit.Assert.Equal(calls[0].IdempotencyKey, calls[1].IdempotencyKey);
        Xunit.Assert.NotEqual(calls[0].IdempotencyKey, calls[2].IdempotencyKey);
        var first = Xunit.Assert.IsType<Diary.CopyNutritionDiary>(calls[0].Payload);
        Xunit.Assert.Equal(SourceDate, first.SourceDate);
        Xunit.Assert.Equal(TargetDate, first.TargetDate);
        Xunit.Assert.Equal(Diary.MealType.Breakfast, first.MealType);
        Xunit.Assert.All(api.Calls.Where(call => call.Method == "GET"),
            call => Xunit.Assert.Contains($"date={SourceDate:yyyy-MM-dd}", call.Path));
    }

    [Xunit.Fact]
    public async Task DayMapsSnapshotAndGroupedMealWithoutDroppingTotals()
    {
        var api = new RecordingApiClient
        {
            Day = Day([Entry("snapshot:" + ItemId.ToString("N"), 120), Entry("meal:" + GroupedMealId.ToString("N"), 180)])
        };
        var result = await new CloudNutritionDailyLogStore(api).GetLogForDateAsync(SourceDate);

        Xunit.Assert.Equal(2, result.Entries.Count);
        Xunit.Assert.Contains(result.Entries, item => item.Id == ItemId);
        Xunit.Assert.Contains(result.Entries, item => item.Id == GroupedMealId);
        Xunit.Assert.Equal(300, result.TotalCalories);
    }

    [Xunit.Fact]
    public async Task DiaryFailureIsNotReportedAsSuccess()
    {
        var api = new RecordingApiClient { MutationStatus = BackendApiStatus.Conflict };
        var exception = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudNutritionDailyLogStore(api).BatchSaveFoodItemsAsync(SourceDate, [Draft(120)]));
        Xunit.Assert.Contains("Conflict", exception.Message);
    }

    [Xunit.Fact]
    public async Task CatalogSearchReadsAllServerPagesBeforeClientPagination()
    {
        var api = new RecordingApiClient
        {
            CatalogPages =
            [
                new FoodCatalogSearchResponse([CatalogItem(1, "Skyr plain", "catalog:1")], 1, 50, true),
                new FoodCatalogSearchResponse([CatalogItem(2, "Skyr berry", "catalog:2")], 2, 50, false)
            ]
        };

        var items = await new CloudFoodCatalogSearchStore(api).SearchCandidatesAsync("skyr & berry");

        Xunit.Assert.Equal(2, items.Count);
        Xunit.Assert.Equal([1, 2], items.Select(item => item.Id));
        Xunit.Assert.Contains("query=skyr%20%26%20berry", api.Calls[0].Path);
        Xunit.Assert.Contains("pageNumber=2", api.Calls[1].Path);
    }

    private static LoggedFoodItemDto Draft(double calories) => new(ItemId, Guid.Empty, MealType.Breakfast,
        "Synthetic bowl", 100, "g", 1, calories, 12, 15, 3, LoggedAtUtc, []);

    private static Diary.NutritionDiaryEntry Entry(string identity, decimal calories) => new(identity, SourceDate,
        Diary.MealType.Breakfast, "Synthetic bowl", 100, "g", 1, calories, 12, 15, 3, LoggedAtUtc, true);

    private static Diary.NutritionDiaryDay Day(IReadOnlyList<Diary.NutritionDiaryEntry> entries) => new(
        Guid.Parse("50000000-0000-0000-0000-000000000003"), SourceDate, 0, null, 1800, 100, 200, 60,
        entries, null);

    private static FoodCatalogItemDto CatalogItem(int id, string name, string key) => new(
        id, name, 100, 10, 12, 2, 100, "g", null, "Synthetic catalog", "100 kcal", false, key);

    private sealed class RecordingApiClient : IMuscleCutiesBackendApiClient
    {
        public Diary.NutritionDiaryDay Day { get; set; } = CloudNutritionContractTests.Day([]);
        public BackendApiStatus MutationStatus { get; set; } = BackendApiStatus.Success;
        public IReadOnlyList<FoodCatalogSearchResponse>? CatalogPages { get; set; }
        public List<(string Method, string Path, object? Payload, string? IdempotencyKey)> Calls { get; } = [];

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(("GET", relativePath, null, null));
            object result = CatalogPages is not null && relativePath.Contains("/catalog/search", StringComparison.Ordinal)
                ? CatalogPages[Calls.Count - 1]
                : Day;
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)result));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((method.Method, relativePath, payload, idempotencyKey));
            object? response = relativePath.EndsWith("/copy", StringComparison.Ordinal)
                ? new Diary.CopyNutritionDiaryResponse(1, ["snapshot:" + ItemId.ToString("N")])
                : default(JsonElement);
            return Task.FromResult(new BackendApiResponse<TResponse>(MutationStatus,
                MutationStatus == BackendApiStatus.Success ? (TResponse?)response : default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath,
            string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
