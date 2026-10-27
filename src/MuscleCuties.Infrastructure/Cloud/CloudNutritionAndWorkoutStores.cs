using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Workout.Archive;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using DiaryMealType = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType;
using DiaryContracts = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudNutritionOverviewStore(
    IMuscleCutiesBackendApiClient apiClient) : INutritionOverviewStore
{
    public async Task<NutritionOverviewResponse> GetOverviewAsync(
        NutritionOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<NutritionOverviewResponse>(
            $"/api/v1/nutrition/overview?date={request.Date:yyyy-MM-dd}&breakfastPreference={Uri.EscapeDataString(request.BreakfastPreference)}",
            cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "nutrition overview");
    }

    public async Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(string breakfastPreference, CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(new NutritionOverviewRequest(DateOnly.FromDateTime(DateTime.Now), breakfastPreference), cancellationToken)
            .ConfigureAwait(false);
        return overview.Targets;
    }

    public async Task LogMealAsync(LogMealRequest request, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<LogMealRequest, object>(
            HttpMethod.Post, "/api/v1/nutrition/meals", request, cancellationToken: cancellationToken).ConfigureAwait(false), "log meal");

    public async Task UpdateMealAsync(UpdateMealRequest request, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<UpdateMealRequest, object>(
            HttpMethod.Put, $"/api/v1/nutrition/meals/{request.MealId}", request, cancellationToken: cancellationToken).ConfigureAwait(false), "update meal");

    public async Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(int mealId, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<MealIngredientInputDto>>(
            $"/api/v1/nutrition/meals/{mealId}/ingredients", cancellationToken).ConfigureAwait(false), "meal ingredients");

    public async Task<CustomFoodResultDto> CreateCustomFoodAsync(CreateCustomFoodRequest request, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<CreateCustomFoodRequest, CustomFoodResultDto>(
            HttpMethod.Post, "/api/v1/nutrition/custom-foods", request, cancellationToken: cancellationToken).ConfigureAwait(false), "create custom food");

    public async Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(string mealType, string breakfastPreference, IReadOnlySet<string>? excludeConceptNames, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<List<MealSuggestionDto>>(
            $"/api/v1/nutrition/suggestions?mealType={Uri.EscapeDataString(mealType)}&breakfastPreference={Uri.EscapeDataString(breakfastPreference)}",
            cancellationToken).ConfigureAwait(false);
        var suggestions = CloudStoreResponse.RequireData(response, "meal suggestions");
        return excludeConceptNames is null ? suggestions : suggestions.Where(item => !excludeConceptNames.Contains(item.ConceptName)).ToArray();
    }

    public async Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(string breakfastPreference, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<List<DayMealPlanItemDto>>(
            $"/api/v1/nutrition/meal-plan?date={DateOnly.FromDateTime(DateTime.Now):yyyy-MM-dd}&breakfastPreference={Uri.EscapeDataString(breakfastPreference)}",
            cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "day meal plan");
    }

    public async Task<DayMealPlanItemDto> RefreshDayMealAsync(DayMealPlanItemDto item, string breakfastPreference, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.SendMutationAsync<DayMealPlanItemDto, DayMealPlanItemDto>(
            HttpMethod.Post,
            $"/api/v1/nutrition/meal-plan/refresh?breakfastPreference={Uri.EscapeDataString(breakfastPreference)}",
            item, cancellationToken: cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "refresh day meal");
    }

    public async Task EnsureReferenceDataReadyAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<FoodCatalogSearchResponse>(
            "/api/v1/nutrition/catalog/search?query=a&pageSize=1", cancellationToken).ConfigureAwait(false);
        CloudStoreResponse.RequireData(response, "food catalog readiness");
    }
}

public sealed class CloudNutritionDailyLogStore(
    IMuscleCutiesBackendApiClient apiClient) : INutritionDailyLogStore
{
    private const string DiaryRoute = "/api/v1/nutrition/diary";
    private const int DiaryPageSize = 100;
    private const int GuidByteCount = 16;
    private const int RecentHistoryPageSize = 7;
    private const int MaximumRecentItems = 12;
    private const string SnapshotIdentityPrefix = "snapshot:";
    private const string BatchCommandName = "batch";
    private const string CopyCommandName = "copy";
    private const string CopyAllMealsKey = "all-meals";

    public async Task<DailyNutritionLogSnapshot> GetLogForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var entries = new List<LoggedFoodItemDto>();
        string? pageToken = null;
        var seenPageTokens = new HashSet<string>(StringComparer.Ordinal);
        DiaryContracts.NutritionDiaryDay? day = null;
        do
        {
            var path = $"{DiaryRoute}/day?date={date:yyyy-MM-dd}&pageSize={DiaryPageSize}";
            if (pageToken is not null) path += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            day = CloudStoreResponse.RequireData(await apiClient.GetAsync<DiaryContracts.NutritionDiaryDay>(path, cancellationToken)
                .ConfigureAwait(false), "nutrition diary day");
            entries.AddRange(day.Entries.Select(entry => MapEntry(entry, day.LogId)));
            if (day.NextPageToken is not null && !seenPageTokens.Add(day.NextPageToken))
                throw new InvalidOperationException("Nutrition diary pagination did not advance.");
            pageToken = day.NextPageToken;
        } while (pageToken is not null);

        var createdAt = day!.CreatedAtUtc ?? DateTime.MinValue;
        var updatedAt = day.UpdatedAtUtc ?? DateTime.MinValue;
        return new DailyNutritionLogSnapshot(day.LogId, date, day.WaterIntakeMl,
            (double)day.TargetCalories, (double)day.TargetProtein, (double)day.TargetCarbs, (double)day.TargetFat,
            entries.Sum(item => item.Calories), entries.Sum(item => item.Protein), entries.Sum(item => item.Carbs), entries.Sum(item => item.Fat),
            day.Notes, createdAt, updatedAt, entries);
    }

    public async Task<LoggedFoodItemDto?> GetFoodItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<DiaryContracts.NutritionDiaryEntry>(
            $"{DiaryRoute}/entries/{id:D}", cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound) return null;
        var entry = CloudStoreResponse.RequireData(response, "nutrition diary item");
        var day = await GetLogForDateAsync(entry.Date, cancellationToken).ConfigureAwait(false);
        return day.Entries.FirstOrDefault(item => item.Id == id);
    }

    public async Task SaveFoodItemAsync(DateOnly date, LoggedFoodItemDto item, bool requireExisting, CancellationToken cancellationToken = default)
    {
        if (item.Id == Guid.Empty)
            throw new ArgumentException("A stable diary item ID is required for cloud writes.", nameof(item));
        var identity = SnapshotIdentityPrefix + item.Id.ToString("N");
        var request = new DiaryContracts.SaveNutritionDiaryEntry(identity, date, (DiaryMealType)(int)item.MealType,
            item.Name, (decimal)item.ServingSize, item.ServingUnit, (decimal)item.ServingsCount,
            (decimal)item.Calories, (decimal)item.Protein, (decimal)item.Carbs, (decimal)item.Fat,
            ToUtc(item.LoggedAtTimestamp), RequireExisting: requireExisting);
        CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<DiaryContracts.SaveNutritionDiaryEntry, DiarySaveResponse>(
            HttpMethod.Put, $"{DiaryRoute}/entries", request, cancellationToken: cancellationToken).ConfigureAwait(false), "save nutrition diary item");
    }

    public async Task BatchSaveFoodItemsAsync(DateOnly date, IReadOnlyList<LoggedFoodItemDto> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Any(item => item.Id == Guid.Empty))
            throw new ArgumentException("Stable diary item IDs are required for cloud batch writes.", nameof(items));
        // The server compares a key against the complete request body. Include every mutable
        // value so a later edit of the same draft IDs is a new command, while retries replay.
        var requestId = StableRequestId($"{BatchCommandName}:{date:yyyy-MM-dd}:{JsonSerializer.Serialize(items)}");
        var request = new DiaryContracts.BatchSaveNutritionDiaryEntries(requestId, date,
            items.Select(item => new DiaryContracts.SaveNutritionDiaryEntry(null, date, (DiaryMealType)(int)item.MealType,
                item.Name, (decimal)item.ServingSize, item.ServingUnit, (decimal)item.ServingsCount,
                (decimal)item.Calories, (decimal)item.Protein, (decimal)item.Carbs, (decimal)item.Fat,
                ToUtc(item.LoggedAtTimestamp))).ToArray());
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<DiaryContracts.BatchSaveNutritionDiaryEntries, JsonElement>(
            HttpMethod.Post, $"{DiaryRoute}/batch", request, request.RequestId.ToString("N"), cancellationToken).ConfigureAwait(false), "batch save nutrition diary");
    }

    public async Task<int> CopyEntriesAsync(DateOnly sourceDate, DateOnly targetDate, MealType? mealType, CancellationToken cancellationToken = default)
    {
        var source = await GetLogForDateAsync(sourceDate, cancellationToken).ConfigureAwait(false);
        var requestId = StableRequestId($"{CopyCommandName}:{sourceDate:yyyy-MM-dd}:{targetDate:yyyy-MM-dd}:{mealType?.ToString() ?? CopyAllMealsKey}:{JsonSerializer.Serialize(source.Entries)}");
        var request = new DiaryContracts.CopyNutritionDiary(requestId, sourceDate, targetDate,
            mealType.HasValue ? (DiaryMealType)(int)mealType.Value : null);
        var response = await apiClient.SendMutationAsync<DiaryContracts.CopyNutritionDiary, DiaryContracts.CopyNutritionDiaryResponse>(
            HttpMethod.Post, $"{DiaryRoute}/copy", request, request.RequestId.ToString("N"), cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "copy nutrition diary").CopiedCount;
    }

    public async Task DeleteFoodItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<DiaryContracts.NutritionDiaryEntry>(
            $"{DiaryRoute}/entries/{itemId:D}", cancellationToken).ConfigureAwait(false);
        var entry = CloudStoreResponse.RequireData(response, "nutrition diary item");
        CloudStoreResponse.RequireSuccess(await apiClient.DeleteAsync(
            $"{DiaryRoute}/entries/{Uri.EscapeDataString(entry.Identity)}?date={entry.Date:yyyy-MM-dd}",
            cancellationToken: cancellationToken).ConfigureAwait(false), "delete nutrition diary item");
    }

    public Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemSnapshot quickItem, LoggedFoodItemDto draft, CancellationToken cancellationToken = default) =>
        SaveFoodItemAsync(date, draft with { MealType = mealType }, false, cancellationToken);

    public async Task<IReadOnlyList<DailyNutritionSummarySnapshot>> GetDateRangeSummariesAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end));
        var summaries = new List<DailyNutritionSummarySnapshot>();
        var beforeDate = end.AddDays(1);
        while (beforeDate > start)
        {
            var response = await apiClient.GetAsync<DiaryContracts.NutritionDiaryHistory>(
                $"{DiaryRoute}/history?beforeDate={beforeDate:yyyy-MM-dd}&pageSize={DiaryPageSize}", cancellationToken)
                .ConfigureAwait(false);
            var history = CloudStoreResponse.RequireData(response, "nutrition diary history");
            foreach (var row in history.Days.Where(row => row.Date >= start && row.Date <= end))
            {
                var day = await GetLogForDateAsync(row.Date, cancellationToken).ConfigureAwait(false);
                summaries.Add(new DailyNutritionSummarySnapshot(row.Date, day.Id, (double)row.Calories,
                    (double)row.Protein, (double)row.Carbs, (double)row.Fat, day.TargetCalories,
                    row.WaterIntakeMl, row.ItemsCount));
            }
            if (history.NextBeforeDate is null || history.NextBeforeDate >= beforeDate)
                break;
            beforeDate = history.NextBeforeDate.Value;
        }
        return summaries;
    }

    public async Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl, CancellationToken cancellationToken = default)
    {
        var day = await GetLogForDateAsync(date, cancellationToken).ConfigureAwait(false);
        await UpdateMetadataAsync(date, waterIntakeMl, day.Notes, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken cancellationToken = default)
    {
        var day = await GetLogForDateAsync(date, cancellationToken).ConfigureAwait(false);
        await UpdateMetadataAsync(date, day.WaterIntakeMl, notes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentIngredientsAsync(DateOnly date, MealType mealType, CancellationToken cancellationToken = default) =>
        (await GetRecentEntriesAsync(date, mealType, cancellationToken).ConfigureAwait(false))
        .Where(entry => entry.AcceptedSuggestionName is null)
        .DistinctBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        .Take(MaximumRecentItems)
        .Select(ToQuickItem).ToArray();

    public async Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentAcceptedSuggestionsAsync(DateOnly date, MealType mealType, CyclePhase phase, CancellationToken cancellationToken = default) =>
        (await GetRecentEntriesAsync(date, mealType, cancellationToken).ConfigureAwait(false))
        .Where(entry => entry.AcceptedSuggestionName is not null && entry.AcceptedSuggestionPhase == phase)
        .DistinctBy(entry => entry.AcceptedSuggestionName, StringComparer.OrdinalIgnoreCase)
        .Take(MaximumRecentItems)
        .Select(ToQuickItem).ToArray();

    public async Task<CyclePhase> GetPhaseForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<ServerCycleOverview>(
            $"/api/v1/cycle/overview?from={date:yyyy-MM-dd}&through={date:yyyy-MM-dd}&today={date:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);
        var phaseName = CloudStoreResponse.RequireData(response, "cycle phase").Prediction.CurrentPhase;
        return Enum.TryParse<CyclePhase>(phaseName, true, out var phase)
            ? phase : throw new InvalidOperationException("Cloud cycle overview returned an unknown phase.");
    }

    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchLocalFoodsAsync(string query, int maxResults = 12, CancellationToken cancellationToken = default) =>
        SearchCatalogFoodsAsync(query, maxResults, cancellationToken);

    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchOnlineFoodsAsync(string query, int maxResults = 12, CancellationToken cancellationToken = default) =>
        SearchCatalogFoodsAsync(query, maxResults, cancellationToken);

    private async Task<IReadOnlyList<FastLookupFoodItemDto>> SearchCatalogFoodsAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var response = await apiClient.GetAsync<FoodCatalogSearchResponse>(
            $"/api/v1/nutrition/catalog/search?query={Uri.EscapeDataString(query)}&pageSize={maxResults}",
            cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "nutrition catalog lookup").Items
            .Where(item => item.Calories.HasValue && item.Protein.HasValue && item.Carbs.HasValue && item.Fats.HasValue)
            .Select(item => new FastLookupFoodItemDto(item.Name, item.Calories!.Value, item.Protein!.Value,
                item.Carbs!.Value, item.Fats!.Value, item.IsCustom)).ToArray();
    }

    private async Task<IReadOnlyList<LoggedFoodItemDto>> GetRecentEntriesAsync(DateOnly date, MealType mealType, CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<DiaryContracts.NutritionDiaryHistory>(
            $"{DiaryRoute}/history?beforeDate={date.AddDays(1):yyyy-MM-dd}&pageSize={RecentHistoryPageSize}", cancellationToken)
            .ConfigureAwait(false);
        var history = CloudStoreResponse.RequireData(response, "recent nutrition diary history");
        var entries = new List<LoggedFoodItemDto>();
        foreach (var historyDay in history.Days)
        {
            var day = await GetLogForDateAsync(historyDay.Date, cancellationToken).ConfigureAwait(false);
            entries.AddRange(day.Entries.Where(entry => entry.MealType == mealType));
        }
        return entries.OrderByDescending(entry => entry.LoggedAtTimestamp).ToArray();
    }

    private static QuickFoodItemSnapshot ToQuickItem(LoggedFoodItemDto entry) => new(
        entry.Name, entry.ServingSize, entry.ServingUnit, entry.ServingsCount,
        entry.Calories, entry.Protein, entry.Carbs, entry.Fat, entry.Name, entry.Id,
        Ingredients: entry.Ingredients);

    private async Task UpdateMetadataAsync(DateOnly date, int waterIntakeMl, string? notes, CancellationToken cancellationToken)
    {
        var request = new DiaryContracts.UpdateNutritionDiaryMetadata(date, waterIntakeMl, notes);
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<DiaryContracts.UpdateNutritionDiaryMetadata, object>(
            HttpMethod.Put, $"{DiaryRoute}/metadata", request, cancellationToken: cancellationToken).ConfigureAwait(false), "update nutrition diary metadata");
    }

    private static LoggedFoodItemDto MapEntry(DiaryContracts.NutritionDiaryEntry entry, Guid logId)
    {
        var identity = entry.Identity ?? throw new InvalidOperationException("Nutrition diary returned an invalid item identity.");
        var separator = identity.IndexOf(':');
        if (separator < 0 || !Guid.TryParse(identity[(separator + 1)..], out var id) ||
            (identity[..separator] != "snapshot" && identity[..separator] != "meal"))
            throw new InvalidOperationException("Nutrition diary returned an invalid item identity.");
        return new LoggedFoodItemDto(id, logId, (MealType)(int)entry.MealType, entry.Name,
            (double)entry.ServingSize, entry.ServingUnit, (double)entry.ServingsCount,
            (double)entry.Calories, (double)entry.Protein, (double)entry.Carbs, (double)entry.Fat,
            entry.LoggedAtUtc, [], entry.AcceptedSuggestionName,
            Enum.TryParse<CyclePhase>(entry.AcceptedSuggestionPhase, true, out var phase) ? phase : null);
    }

    private sealed record DiarySaveResponse(string Identity);

    private static Guid StableRequestId(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, GuidByteCount));
    }

    private static DateTime ToUtc(DateTime timestamp) =>
        (timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Local)
            : timestamp).ToUniversalTime();
}

public sealed class CloudFoodCatalogSearchStore(
    IMuscleCutiesBackendApiClient apiClient) : IFoodCatalogSearchStore, IBarcodeFoodLookupStore
{
    private const string CustomFoodKeyPrefix = "custom:";
    private const int CatalogPageSize = 50;
    private const int FirstCatalogPage = 1;
    public async Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var candidates = new List<FoodCatalogCandidate>();
        for (var page = FirstCatalogPage; ; page++)
        {
            var response = await apiClient.GetAsync<FoodCatalogSearchResponse>(
                $"/api/v1/nutrition/catalog/search?query={Uri.EscapeDataString(query)}&pageSize={CatalogPageSize}&pageNumber={page}",
                cancellationToken).ConfigureAwait(false);
            var result = CloudStoreResponse.RequireData(response, "food catalog search");
            if (result.PageNumber != page || (result.HasMore && result.Items.Count == 0))
                throw new InvalidOperationException("Food catalog pagination did not advance.");
            candidates.AddRange(result.Items.Select(item => MapCatalogItem(item)));
            if (!result.HasMore) return candidates;
        }
    }

    public async Task<FoodCatalogCandidate?> FindFoodByBarcodeAsync(
        IReadOnlyList<string> exactValues,
        CancellationToken cancellationToken = default)
    {
        if (exactValues == null || exactValues.Count == 0)
            return null;

        foreach (var barcode in exactValues)
        {
            var response = await apiClient.SendMutationAsync<BarcodeLookupRequest, BarcodeLookupResult>(
                HttpMethod.Post, "/api/v1/nutrition/catalog/barcode", new BarcodeLookupRequest(barcode),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.Status == BackendApiStatus.NotFound) continue;
            var result = CloudStoreResponse.RequireData(response, "barcode lookup");
            if (result.FoodItem is not null) return MapCatalogItem(result.FoodItem, result.NormalizedBarcode);
        }
        return null;
    }

    private static FoodCatalogCandidate MapCatalogItem(FoodCatalogItemDto item, string? barcode = null)
    {
        Guid? customId = null;
        if (item.FoodKey.StartsWith(CustomFoodKeyPrefix, StringComparison.Ordinal))
        {
            if (!Guid.TryParse(item.FoodKey[CustomFoodKeyPrefix.Length..], out var parsedId))
                throw new InvalidOperationException("Cloud food catalog returned an invalid custom food key.");
            customId = parsedId;
        }
        return new FoodCatalogCandidate(item.Id, item.Name, item.Calories, item.Protein, item.Carbs, item.Fats,
            item.IsCustom, null, item.SourceSummary, null, null, barcode, null,
            item.ServingSize, item.ServingSizeUnit, item.ServingOptionsJson, customId);
    }
}

public sealed class CloudRecipeStore(
    IMuscleCutiesBackendApiClient apiClient) : IRecipeStore
{
    private const string NutritionRoute = "/api/v1/nutrition";

    public async Task<RecipeDetailsDto> SaveRecipeAsync(RecipeDetailsDto recipe, CancellationToken cancellationToken = default)
    {
        var request = new CreateRecipeRequest(recipe.Name, recipe.Description, recipe.ServingsCount, recipe.CookedWeightGrams,
            recipe.Ingredients.Select(ToIngredientInput).ToArray());
        return CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<CreateRecipeRequest, RecipeDetailsDto>(
            HttpMethod.Post, $"{NutritionRoute}/recipes", request, cancellationToken: cancellationToken).ConfigureAwait(false), "save recipe");
    }

    public async Task<RecipeDetailsDto> UpdateRecipeAsync(RecipeDetailsDto recipe, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var request = new UpdateRecipeRequest(recipe.Id, expectedVersion, recipe.Name, recipe.Description,
            recipe.ServingsCount, recipe.CookedWeightGrams, recipe.Ingredients.Select(ToIngredientInput).ToArray());
        return CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<UpdateRecipeRequest, RecipeDetailsDto>(
            HttpMethod.Put, $"{NutritionRoute}/recipes/{recipe.Id:D}", request, cancellationToken: cancellationToken).ConfigureAwait(false), "update recipe");
    }

    private static RecipeIngredientInputDto ToIngredientInput(RecipeIngredientDto ingredient) => new(
        ingredient.FoodItemId, ingredient.Name, ingredient.Grams, ingredient.Calories, ingredient.Protein,
        ingredient.Carbs, ingredient.Fats, ingredient.Fiber, ingredient.SugarAlcohols,
        ingredient.SourceKind, ingredient.SourceLabel, ingredient.SourceVersion);

    public async Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        (await GetBundleAsync(null, cancellationToken).ConfigureAwait(false)).Recipes;

    public async Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<RecipeDetailsDto>($"{NutritionRoute}/recipes/{id:D}", cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound) return null;
        return CloudStoreResponse.RequireData(response, "recipe detail");
    }

    public async Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.DeleteAsync(
            $"{NutritionRoute}/recipes/{id:D}", cancellationToken: cancellationToken).ConfigureAwait(false), "delete recipe");

    public async Task<MealTemplateDto> SaveMealTemplateAsync(MealTemplateDto template, CancellationToken cancellationToken = default)
    {
        var request = new SaveMealAsTemplateRequest(template.Name, template.MealType, template.Items);
        return CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<SaveMealAsTemplateRequest, MealTemplateDto>(
            HttpMethod.Post, $"{NutritionRoute}/templates", request, cancellationToken: cancellationToken).ConfigureAwait(false), "save meal template");
    }

    public async Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(int? mealType = null, CancellationToken cancellationToken = default) =>
        (await GetBundleAsync(mealType, cancellationToken).ConfigureAwait(false)).Templates;

    public async Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<MealTemplateDto>($"{NutritionRoute}/templates/{id:D}", cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound) return null;
        return CloudStoreResponse.RequireData(response, "meal template detail");
    }

    public async Task DeleteMealTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.DeleteAsync(
            $"{NutritionRoute}/templates/{id:D}", cancellationToken: cancellationToken).ConfigureAwait(false), "delete meal template");

    private async Task<RecipesAndTemplatesBundleDto> GetBundleAsync(int? mealType, CancellationToken cancellationToken)
    {
        var path = mealType.HasValue ? $"{NutritionRoute}/recipes?mealType={mealType.Value}" : $"{NutritionRoute}/recipes";
        return CloudStoreResponse.RequireData(await apiClient.GetAsync<RecipesAndTemplatesBundleDto>(path, cancellationToken)
            .ConfigureAwait(false), "recipes and templates");
    }
}

public sealed class CloudWorkoutOverviewStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutOverviewStore
{
    public async Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutOverviewResponse>(
            $"/api/v1/workouts/overview?startDate={request.StartDate:yyyy-MM-dd}", cancellationToken).ConfigureAwait(false), "workout overview");

    public async Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<RescheduleWorkoutDayRequest, object>(
            HttpMethod.Post, "/api/v1/workouts/reschedule", request, cancellationToken: cancellationToken).ConfigureAwait(false), "reschedule workout day");

    public async Task SetSessionOptionalAsync(SetWorkoutSessionOptionalRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SetWorkoutSessionOptionalRequest, object>(
            HttpMethod.Put, "/api/v1/workouts/optional-session", request, cancellationToken: cancellationToken).ConfigureAwait(false), "mark optional workout session");

    public async Task<ReadinessAdjustmentProposalDto> ReviewReadinessAdjustmentAsync(ReviewReadinessAdjustmentRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<ReviewReadinessAdjustmentRequest, ReadinessAdjustmentProposalDto>(
            HttpMethod.Post, "/api/v1/workouts/readiness-proposals/review", request, cancellationToken: cancellationToken).ConfigureAwait(false), "review readiness adjustment");

    public async Task<ReadinessAdjustmentDecisionDto> DecideReadinessAdjustmentAsync(DecideReadinessAdjustmentRequest request, bool apply, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<DecideReadinessAdjustmentRequest, ReadinessAdjustmentDecisionDto>(
            HttpMethod.Post, apply ? "/api/v1/workouts/readiness-proposals/apply" : "/api/v1/workouts/readiness-proposals/keep",
            request, cancellationToken: cancellationToken).ConfigureAwait(false), apply ? "apply readiness adjustment" : "keep workout plan");
}

public sealed class CloudWorkoutSessionActionStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutSessionActionStore
{
    public async Task<IReadOnlyList<PlannedWorkoutSessionHistoryEntry>> GetPlannedSessionHistoryAsync(int workoutDayId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<PlannedWorkoutSessionHistoryEntry>>(
            $"/api/v1/workouts/sessions/planned/{workoutDayId}/history", cancellationToken).ConfigureAwait(false), "read workout session history");
    public async Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(int workoutDayId, CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<ActiveWorkoutSessionSnapshot>($"/api/v1/workouts/sessions/planned/{workoutDayId}", cancellationToken).ConfigureAwait(false);
        return response.Status == BackendApiStatus.NotFound ? null : CloudStoreResponse.RequireData(response, "read planned workout session");
    }

    public async Task<ActiveWorkoutSessionSnapshot> StartPlannedSessionAsync(int workoutDayId, Guid sessionId, DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        var request = new StartWorkoutSessionCommand(sessionId.ToString("N"), sessionId, "Workout", startedAtUtc, PlannedWorkoutDayId: workoutDayId);
        var response = await apiClient.SendMutationAsync<StartWorkoutSessionCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, "/api/v1/workouts/sessions/start", request, idempotencyKey: sessionId.ToString("N"), cancellationToken).ConfigureAwait(false);
        return RequirePlannedSession(response, "start planned workout session");
    }

    public async Task<ActiveWorkoutSessionSnapshot> FinishPlannedSessionAsync(Guid sessionId, DateTime completedAtUtc, int totalDurationSeconds, CancellationToken cancellationToken)
    {
        var request = new FinishWorkoutSessionCommand(sessionId.ToString("N"), sessionId, completedAtUtc, totalDurationSeconds, null, null);
        var response = await apiClient.SendMutationAsync<FinishWorkoutSessionCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, $"/api/v1/workouts/sessions/{sessionId:D}/finish", request, idempotencyKey: sessionId.ToString("N"), cancellationToken).ConfigureAwait(false);
        return RequirePlannedSession(response, "finish planned workout session");
    }

    public async Task AbandonPlannedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var request = new CancelWorkoutSessionCommand(sessionId.ToString("N"), sessionId);
        var response = await apiClient.SendMutationAsync<CancelWorkoutSessionCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, $"/api/v1/workouts/sessions/{sessionId:D}/cancel", request, idempotencyKey: sessionId.ToString("N"), cancellationToken).ConfigureAwait(false);
        RequirePlannedSession(response, "abandon planned workout session");
    }

    public async Task<ActiveWorkoutSessionSnapshot> ResumePlannedTimingAsync(Guid sessionId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var key = operationId.ToString("N");
        var response = await apiClient.SendMutationAsync<ResumeWorkoutTimingCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, $"/api/v1/workouts/sessions/{sessionId:D}/timing/resume",
            new ResumeWorkoutTimingCommand(key, sessionId), idempotencyKey: key, cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == "WorkoutActivityNoLongerSafe")
            throw new WorkoutActivityNoLongerSafeException();
        return RequirePlannedSession(response, "resume workout timing");
    }

    public async Task<ActiveWorkoutSessionSnapshot> CheckpointPlannedTimingAsync(Guid sessionId, Guid operationId,
        Guid intervalId, int measuredSeconds, bool pause, CancellationToken cancellationToken)
    {
        var key = operationId.ToString("N");
        var response = await apiClient.SendMutationAsync<CheckpointWorkoutTimingCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, $"/api/v1/workouts/sessions/{sessionId:D}/timing/checkpoint",
            new CheckpointWorkoutTimingCommand(key, sessionId, intervalId, measuredSeconds, pause),
            idempotencyKey: key, cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == "WorkoutTimingIntervalExpired")
            throw new WorkoutTimingIntervalExpiredException();
        return RequirePlannedSession(response, "checkpoint workout timing");
    }

    private static ActiveWorkoutSessionSnapshot RequirePlannedSession(
        BackendApiResponse<ActiveWorkoutSessionSnapshot> response, string operation)
    {
        if (response.Status == BackendApiStatus.Conflict &&
            response.ErrorCode == WorkoutHiitHistoryConfirmationRequiredException.ErrorCode)
            throw new WorkoutHiitHistoryConfirmationRequiredException();
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == "WorkoutActivityNoLongerSafe")
            throw new WorkoutActivityNoLongerSafeException();
        if (response.Status == BackendApiStatus.OfflineRejected)
            throw new HttpRequestException("Workout session requires a connection.");
        return CloudStoreResponse.RequireData(response, operation);
    }
    public async Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutSessionDetailDto>(
            $"/api/v1/workouts/day-sessions/{workoutDayId}", cancellationToken).ConfigureAwait(false), "workout session detail");

    public async Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<LogWorkoutSessionRequest, object>(
            HttpMethod.Post, "/api/v1/workouts/day-sessions/log", request, cancellationToken: cancellationToken).ConfigureAwait(false), "log workout session");

    public async Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(int workoutDayExerciseId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<WorkoutSwapCandidateDto>>(
            $"/api/v1/workouts/day-exercises/{workoutDayExerciseId}/swaps", cancellationToken).ConfigureAwait(false), "workout swap candidates");

    public async Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SwapWorkoutExerciseRequest, object>(
            HttpMethod.Post, "/api/v1/workouts/day-exercises/swap", request, cancellationToken: cancellationToken).ConfigureAwait(false), "swap workout exercise");

    public async Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutMovementNoteDto>(
            $"/api/v1/workouts/day-exercises/{workoutDayExerciseId}/movement-note", cancellationToken).ConfigureAwait(false), "workout movement note");

    public async Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SaveWorkoutMovementNoteRequest, object>(
            HttpMethod.Put, "/api/v1/workouts/day-exercises/movement-note", request, cancellationToken: cancellationToken).ConfigureAwait(false), "save workout movement note");

    public async Task SetExerciseOptionalAsync(SetWorkoutExerciseOptionalRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SetWorkoutExerciseOptionalRequest, object>(
            HttpMethod.Put, "/api/v1/workouts/optional-exercise", request, cancellationToken: cancellationToken).ConfigureAwait(false), "mark optional workout exercise");
}

public sealed class CloudWorkoutArchiveStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutArchiveStore
{
    private const string HistoryRoute = "/api/v1/workouts/history";

    public async Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(int limit, int offset, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<WorkoutArchiveSummaryDto>>(
            $"{HistoryRoute}?limit={limit}&offset={offset}", ct).ConfigureAwait(false), "workout history");

    public async Task<WorkoutArchiveDetailDto> GetWorkoutSessionDetailAsync(Guid sessionId, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutArchiveDetailDto>(
            $"{HistoryRoute}/sessions/{sessionId:D}", ct).ConfigureAwait(false), "workout session detail");

    public async Task UpdateWorkoutSessionNotesAsync(Guid sessionId, string notes, int? rpe, CancellationToken ct = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<UpdateWorkoutSessionNotesRequest, object>(
            HttpMethod.Put, $"{HistoryRoute}/sessions/{sessionId:D}/notes", new UpdateWorkoutSessionNotesRequest(notes, rpe),
            cancellationToken: ct).ConfigureAwait(false), "update workout notes");

    public async Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(int year, int month, int limit = 100, int offset = 0, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<WorkoutArchiveSummaryDto>>(
            $"{HistoryRoute}/month/{year}/{month}?limit={limit}&offset={offset}", ct).ConfigureAwait(false), "monthly workout history");

    public async Task UpdateExerciseSetAsync(Guid setId, double weightKg, int repsCompleted, CancellationToken ct = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<UpdateExerciseSetRequest, object>(
            HttpMethod.Put, $"{HistoryRoute}/sets/{setId:D}", new UpdateExerciseSetRequest(weightKg, repsCompleted),
            cancellationToken: ct).ConfigureAwait(false), "update workout set");

    public async Task<int> GetTotalSessionsCountAsync(CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutHistoryCountDto>(
            $"{HistoryRoute}/count", ct).ConfigureAwait(false), "workout history count").Count;

    public async Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<HistoryMonthOptionDto>>(
            $"{HistoryRoute}/months", ct).ConfigureAwait(false), "workout history months");

    public async Task<int> GetWorkoutHistoryCountByMonthAsync(int year, int month, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutHistoryCountDto>(
            $"{HistoryRoute}/month/{year}/{month}/count", ct).ConfigureAwait(false), "monthly workout count").Count;
}

internal static class CloudStoreResponse
{
    public static T RequireData<T>(BackendApiResponse<T> response, string operation) where T : class
    {
        RequireSuccess(response, operation);
        return response.Data ?? throw new InvalidOperationException($"Cloud {operation} returned no data.");
    }

    public static void RequireSuccess<T>(BackendApiResponse<T> response, string operation)
    {
        if (response.Status != BackendApiStatus.Success)
            throw new InvalidOperationException($"Cloud {operation} failed: {response.Status} ({response.ErrorCode ?? "unknown"}).");
    }
}
