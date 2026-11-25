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

/// <summary>The nutrition overview read through the backend: the plan's targets and the meals it suggests for a day.</summary>
public sealed class CloudNutritionOverviewStore(
    IMuscleCutiesBackendApiClient apiClient) : INutritionOverviewStore
{
    /// <summary>Loads the nutrition overview from the backend.</summary>
    public async Task<NutritionOverviewResponse> GetOverviewAsync(
        NutritionOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<NutritionOverviewResponse>(
            $"/api/v1/nutrition/overview?date={request.Date:yyyy-MM-dd}&breakfastPreference={Uri.EscapeDataString(request.BreakfastPreference)}",
            cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "nutrition overview");
    }

    /// <summary>The macro targets the plan sets for one meal, derived from the plan's own shares rather than split evenly.</summary>
    public async Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(string breakfastPreference, CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(new NutritionOverviewRequest(DateOnly.FromDateTime(DateTime.Now), breakfastPreference), cancellationToken)
            .ConfigureAwait(false);
        return overview.Targets;
    }

    /// <summary>Logs a meal through the backend.</summary>
    public async Task LogMealAsync(LogMealRequest request, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<LogMealRequest, object>(
            HttpMethod.Post, "/api/v1/nutrition/meals", request, cancellationToken: cancellationToken).ConfigureAwait(false), "log meal");

    /// <summary>Updates a logged meal through the backend.</summary>
    public async Task UpdateMealAsync(UpdateMealRequest request, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<UpdateMealRequest, object>(
            HttpMethod.Put, $"/api/v1/nutrition/meals/{request.MealId}", request, cancellationToken: cancellationToken).ConfigureAwait(false), "update meal");

    /// <summary>Loads a planned meal's ingredients.</summary>
    public async Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(int mealId, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<MealIngredientInputDto>>(
            $"/api/v1/nutrition/meals/{mealId}/ingredients", cancellationToken).ConfigureAwait(false), "meal ingredients");

    /// <summary>Creates a custom food through the backend.</summary>
    public async Task<CustomFoodResultDto> CreateCustomFoodAsync(CreateCustomFoodRequest request, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<CreateCustomFoodRequest, CustomFoodResultDto>(
            HttpMethod.Post, "/api/v1/nutrition/custom-foods", request, cancellationToken: cancellationToken).ConfigureAwait(false), "create custom food");

    /// <summary>The meals the plan can suggest for the day.</summary>
    public async Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(string mealType, string breakfastPreference, IReadOnlySet<string>? excludeConceptNames, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<List<MealSuggestionDto>>(
            $"/api/v1/nutrition/suggestions?mealType={Uri.EscapeDataString(mealType)}&breakfastPreference={Uri.EscapeDataString(breakfastPreference)}",
            cancellationToken).ConfigureAwait(false);
        var suggestions = CloudStoreResponse.RequireData(response, "meal suggestions");
        return suggestions
            .Where(item => excludeConceptNames is null || !excludeConceptNames.Contains(item.ConceptName))
            .ToArray();
    }

    /// <summary>The day's planned meals in the order they are eaten.</summary>
    public async Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(string breakfastPreference, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<List<DayMealPlanItemDto>>(
            $"/api/v1/nutrition/meal-plan?date={DateOnly.FromDateTime(DateTime.Now):yyyy-MM-dd}&breakfastPreference={Uri.EscapeDataString(breakfastPreference)}",
            cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "day meal plan");
    }

    /// <summary>Replaces one planned meal with a fresh suggestion and returns it.</summary>
    public async Task<DayMealPlanItemDto> RefreshDayMealAsync(DayMealPlanItemDto item, string breakfastPreference, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.SendMutationAsync<DayMealPlanItemDto, DayMealPlanItemDto>(
            HttpMethod.Post,
            $"/api/v1/nutrition/meal-plan/refresh?breakfastPreference={Uri.EscapeDataString(breakfastPreference)}",
            item, cancellationToken: cancellationToken).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "refresh day meal");
    }

    /// <summary>Makes sure the reference data the overview needs exists, so the first render does not race its seeding.</summary>
    public async Task EnsureReferenceDataReadyAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<FoodCatalogSearchResponse>(
            "/api/v1/nutrition/catalog/search?query=a&pageSize=1", cancellationToken).ConfigureAwait(false);
        CloudStoreResponse.RequireData(response, "food catalog readiness");
    }
}

/// <summary>The diary through the backend: entries per day with the day's water and notes.</summary>
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

    /// <summary>One day's diary.</summary>
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

    /// <summary>One logged entry by its id, or nothing when it has been deleted.</summary>
    public async Task<LoggedFoodItemDto?> GetFoodItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<DiaryContracts.NutritionDiaryEntry>(
            $"{DiaryRoute}/entries/{id:D}", cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound) return null;
        var entry = CloudStoreResponse.RequireData(response, "nutrition diary item");
        var day = await GetLogForDateAsync(entry.Date, cancellationToken).ConfigureAwait(false);
        return day.Entries.FirstOrDefault(item => item.Id == id);
    }

    /// <summary>Writes one entry with a request id derived from its identity, so a retried save updates the same row.</summary>
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

    /// <summary>Writes a day's entries in one call, which is what a copied or imported day needs.</summary>
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

    /// <summary>Copies a day's entries onto another day and reports how many were written.</summary>
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

    /// <summary>Deletes one logged entry.</summary>
    public async Task DeleteFoodItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<DiaryContracts.NutritionDiaryEntry>(
            $"{DiaryRoute}/entries/{itemId:D}", cancellationToken).ConfigureAwait(false);
        var entry = CloudStoreResponse.RequireData(response, "nutrition diary item");
        CloudStoreResponse.RequireSuccess(await apiClient.DeleteAsync(
            $"{DiaryRoute}/entries/{Uri.EscapeDataString(entry.Identity)}?date={entry.Date:yyyy-MM-dd}",
            cancellationToken: cancellationToken).ConfigureAwait(false), "delete nutrition diary item");
    }

    /// <summary>Logs a previously used food again.</summary>
    public Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemSnapshot quickItem, LoggedFoodItemDto draft, CancellationToken cancellationToken = default) =>
        SaveFoodItemAsync(date, draft with { MealType = mealType }, false, cancellationToken);

    /// <summary>Sets the day's water total.</summary>
    public async Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl, CancellationToken cancellationToken = default)
    {
        var day = await GetLogForDateAsync(date, cancellationToken).ConfigureAwait(false);
        await UpdateMetadataAsync(date, waterIntakeMl, day.Notes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets the day's notes.</summary>
    public async Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken cancellationToken = default)
    {
        var day = await GetLogForDateAsync(date, cancellationToken).ConfigureAwait(false);
        await UpdateMetadataAsync(date, day.WaterIntakeMl, notes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The ingredients recently logged for the meal.</summary>
    public async Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentIngredientsAsync(DateOnly date, MealType mealType, CancellationToken cancellationToken = default) =>
        (await GetRecentEntriesAsync(date, mealType, cancellationToken).ConfigureAwait(false))
        .Where(entry => entry.AcceptedSuggestionName is null)
        .DistinctBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        .Take(MaximumRecentItems)
        .Select(ToQuickItem).ToArray();

    /// <summary>The suggestions recently accepted for the meal.</summary>
    public async Task<IReadOnlyList<QuickFoodItemSnapshot>> GetRecentAcceptedSuggestionsAsync(DateOnly date, MealType mealType, CyclePhase phase, CancellationToken cancellationToken = default) =>
        (await GetRecentEntriesAsync(date, mealType, cancellationToken).ConfigureAwait(false))
        .Where(entry => entry.AcceptedSuggestionName is not null && entry.AcceptedSuggestionPhase == phase)
        .DistinctBy(entry => entry.AcceptedSuggestionName, StringComparer.OrdinalIgnoreCase)
        .Take(MaximumRecentItems)
        .Select(ToQuickItem).ToArray();

    /// <summary>The cycle phase on a date, so the diary can show the day in its phase.</summary>
    public async Task<CyclePhase> GetPhaseForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<ServerCycleOverview>(
            $"/api/v1/cycle/overview?from={date:yyyy-MM-dd}&through={date:yyyy-MM-dd}&today={date:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);
        var phaseName = CloudStoreResponse.RequireData(response, "cycle phase").Prediction.CurrentPhase;
        return Enum.TryParse<CyclePhase>(phaseName, true, out var phase)
            ? phase : throw new InvalidOperationException("Cloud cycle overview returned an unknown phase.");
    }

    /// <summary>Searches the local food list.</summary>
    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchLocalFoodsAsync(string query, int maxResults = 12, CancellationToken cancellationToken = default) =>
        SearchCatalogFoodsAsync(query, maxResults, cancellationToken);

    /// <summary>Searches the online catalog.</summary>
    public Task<IReadOnlyList<FastLookupFoodItemDto>> SearchOnlineFoodsAsync(string query, int maxResults = 12, CancellationToken cancellationToken = default) =>
        SearchCatalogFoodsAsync(query, maxResults, cancellationToken);

    /// <summary>Catalog search for the food finder.</summary>
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

    /// <summary>The entry and food pairs the user logged most recently, which is what the quick-add shelf offers.</summary>
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

    /// <summary>Maps a logged entry onto a quick food item.</summary>
    private static QuickFoodItemSnapshot ToQuickItem(LoggedFoodItemDto entry) => new(
        entry.Name, entry.ServingSize, entry.ServingUnit, entry.ServingsCount,
        entry.Calories, entry.Protein, entry.Carbs, entry.Fat, entry.Name, entry.Id,
        Ingredients: entry.Ingredients);

    /// <summary>Writes the day's water and notes together, because they are edited in the same sheet and must land as one</summary>
    /// change.</summary>
    private async Task UpdateMetadataAsync(DateOnly date, int waterIntakeMl, string? notes, CancellationToken cancellationToken)
    {
        var request = new DiaryContracts.UpdateNutritionDiaryMetadata(date, waterIntakeMl, notes);
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<DiaryContracts.UpdateNutritionDiaryMetadata, object>(
            HttpMethod.Put, $"{DiaryRoute}/metadata", request, cancellationToken: cancellationToken).ConfigureAwait(false), "update nutrition diary metadata");
    }

    /// <summary>A diary contract entry as the app's logged item.</summary>
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
            Enum.TryParse<CyclePhase>(entry.AcceptedSuggestionPhase, true, out var phase) ? phase : null,
            entry.LoggedMealId);
    }

    /// <summary>The identity the server gave a saved entry.</summary>
    private sealed record DiarySaveResponse(string Identity);

    /// <summary>A request id derived from content, so the same write sent twice is recognised as one operation.</summary>
    private static Guid StableRequestId(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, GuidByteCount));
    }

    /// <summary>Treats an unspecified timestamp as UTC.</summary>
    private static DateTime ToUtc(DateTime timestamp) =>
        (timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Local)
            : timestamp).ToUniversalTime();
}

/// <summary>Catalog search and lookup through the backend.</summary>
public sealed class CloudFoodCatalogSearchStore(
    IMuscleCutiesBackendApiClient apiClient) : IFoodCatalogSearchStore, IBarcodeFoodLookupStore
{
    private const string CustomFoodKeyPrefix = "custom:";
    private const int CatalogPageSize = 50;
    private const int FirstCatalogPage = 1;
    /// <summary>Searches the food catalog for the query.</summary>
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

    /// <summary>Looks up a food catalog entry by barcode.</summary>
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
            if (response.Status is BackendApiStatus.OfflineRejected or BackendApiStatus.ServerError)
                throw new BarcodeLookupUnavailableException();
            var result = CloudStoreResponse.RequireData(response, "barcode lookup");
            if (result.FoodItem is not null) return MapCatalogItem(result.FoodItem, result.NormalizedBarcode);
        }
        return null;
    }

    /// <summary>A catalog result as the planner's candidate shape.</summary>
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

/// <summary>Recipes and saved meal templates through the backend.</summary>
public sealed class CloudRecipeStore(
    IMuscleCutiesBackendApiClient apiClient) : IRecipeStore
{
    private const string NutritionRoute = "/api/v1/nutrition";

    /// <summary>Saves a new recipe and returns it as stored.</summary>
    public async Task<RecipeDetailsDto> SaveRecipeAsync(RecipeDetailsDto recipe, CancellationToken cancellationToken = default)
    {
        var request = new CreateRecipeRequest(recipe.Name, recipe.Description, recipe.ServingsCount, recipe.CookedWeightGrams,
            recipe.Ingredients.Select(ToIngredientInput).ToArray());
        return CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<CreateRecipeRequest, RecipeDetailsDto>(
            HttpMethod.Post, $"{NutritionRoute}/recipes", request, cancellationToken: cancellationToken).ConfigureAwait(false), "save recipe");
    }

    /// <summary>Saves an existing recipe, keeping its identity.</summary>
    public async Task<RecipeDetailsDto> UpdateRecipeAsync(RecipeDetailsDto recipe, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var request = new UpdateRecipeRequest(recipe.Id, expectedVersion, recipe.Name, recipe.Description,
            recipe.ServingsCount, recipe.CookedWeightGrams, recipe.Ingredients.Select(ToIngredientInput).ToArray());
        return CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<UpdateRecipeRequest, RecipeDetailsDto>(
            HttpMethod.Put, $"{NutritionRoute}/recipes/{recipe.Id:D}", request, cancellationToken: cancellationToken).ConfigureAwait(false), "update recipe");
    }

    /// <summary>Maps a recipe ingredient onto its input shape.</summary>
    private static RecipeIngredientInputDto ToIngredientInput(RecipeIngredientDto ingredient) => new(
        ingredient.FoodItemId, ingredient.Name, ingredient.Grams, ingredient.Calories, ingredient.Protein,
        ingredient.Carbs, ingredient.Fats, ingredient.Fiber, ingredient.SugarAlcohols,
        ingredient.SourceKind, ingredient.SourceLabel, ingredient.SourceVersion);

    /// <summary>The user's saved recipes.</summary>
    public async Task<IReadOnlyList<RecipeSummaryDto>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        (await GetBundleAsync(null, cancellationToken).ConfigureAwait(false)).Recipes;

    /// <summary>One recipe by id, or nothing when it has been deleted.</summary>
    public async Task<RecipeDetailsDto?> GetRecipeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<RecipeDetailsDto>($"{NutritionRoute}/recipes/{id:D}", cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound) return null;
        return CloudStoreResponse.RequireData(response, "recipe detail");
    }

    /// <summary>Deletes a saved recipe.</summary>
    public async Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.DeleteAsync(
            $"{NutritionRoute}/recipes/{id:D}", cancellationToken: cancellationToken).ConfigureAwait(false), "delete recipe");

    /// <summary>Saves a new meal template and returns it as stored.</summary>
    public async Task<MealTemplateDto> SaveMealTemplateAsync(MealTemplateDto template, CancellationToken cancellationToken = default)
    {
        var request = new SaveMealAsTemplateRequest(template.Name, template.MealType, template.Items);
        return CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<SaveMealAsTemplateRequest, MealTemplateDto>(
            HttpMethod.Post, $"{NutritionRoute}/templates", request, cancellationToken: cancellationToken).ConfigureAwait(false), "save meal template");
    }

    /// <summary>Saves an existing template.</summary>
    public async Task<MealTemplateDto> UpdateMealTemplateAsync(MealTemplateDto template, CancellationToken cancellationToken = default)
    {
        var request = new SaveMealAsTemplateRequest(template.Name, template.MealType, template.Items);
        return CloudStoreResponse.RequireData(await apiClient.SendMutationAsync<SaveMealAsTemplateRequest, MealTemplateDto>(
            HttpMethod.Put, $"{NutritionRoute}/templates/{template.Id:D}", request, cancellationToken: cancellationToken).ConfigureAwait(false), "update meal template");
    }

    /// <summary>The saved meal templates, optionally for one meal.</summary>
    public async Task<IReadOnlyList<MealTemplateDto>> GetMealTemplatesAsync(int? mealType = null, CancellationToken cancellationToken = default) =>
        (await GetBundleAsync(mealType, cancellationToken).ConfigureAwait(false)).Templates;

    /// <summary>One template by id.</summary>
    public async Task<MealTemplateDto?> GetMealTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<MealTemplateDto>($"{NutritionRoute}/templates/{id:D}", cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound) return null;
        return CloudStoreResponse.RequireData(response, "meal template detail");
    }

    /// <summary>Deletes a saved meal template.</summary>
    public async Task DeleteMealTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.DeleteAsync(
            $"{NutritionRoute}/templates/{id:D}", cancellationToken: cancellationToken).ConfigureAwait(false), "delete meal template");

    /// <summary>The user's recipes and templates in one read, optionally narrowed to a meal type, which is what the picker</summary>
    /// needs.</summary>
    private async Task<RecipesAndTemplatesBundleDto> GetBundleAsync(int? mealType, CancellationToken cancellationToken)
    {
        var path = mealType.HasValue ? $"{NutritionRoute}/recipes?mealType={mealType.Value}" : $"{NutritionRoute}/recipes";
        return CloudStoreResponse.RequireData(await apiClient.GetAsync<RecipesAndTemplatesBundleDto>(path, cancellationToken)
            .ConfigureAwait(false), "recipes and templates");
    }
}

/// <summary>The workout overview read through the backend.</summary>
public sealed class CloudWorkoutOverviewStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutOverviewStore
{
    /// <summary>Loads the workout overview.</summary>
    public async Task<WorkoutOverviewResponse> LoadAsync(WorkoutOverviewRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutOverviewResponse>(
            $"/api/v1/workouts/overview?startDate={request.StartDate:yyyy-MM-dd}", cancellationToken).ConfigureAwait(false), "workout overview");

    /// <summary>Reschedules a workout day.</summary>
    public async Task RescheduleAsync(RescheduleWorkoutDayRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<RescheduleWorkoutDayRequest, object>(
            HttpMethod.Post, "/api/v1/workouts/reschedule", request, cancellationToken: cancellationToken).ConfigureAwait(false), "reschedule workout day");

    /// <summary>Marks a session optional or required.</summary>
    public async Task SetSessionOptionalAsync(SetWorkoutSessionOptionalRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SetWorkoutSessionOptionalRequest, object>(
            HttpMethod.Put, "/api/v1/workouts/optional-session", request, cancellationToken: cancellationToken).ConfigureAwait(false), "mark optional workout session");
}

/// <summary>The writes an active session makes: starting, finishing and abandoning one.</summary>
public sealed class CloudWorkoutSessionActionStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutSessionActionStore
{
    /// <summary>The planned session's history.</summary>
    public async Task<IReadOnlyList<PlannedWorkoutSessionHistoryEntry>> GetPlannedSessionHistoryAsync(int workoutDayId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<PlannedWorkoutSessionHistoryEntry>>(
            $"/api/v1/workouts/sessions/planned/{workoutDayId}/history", cancellationToken).ConfigureAwait(false), "read workout session history");
    /// <summary>The planned session a workout would start, or nothing when the plan has none today.</summary>
    public async Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(int workoutDayId, CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<ActiveWorkoutSessionSnapshot>($"/api/v1/workouts/sessions/planned/{workoutDayId}", cancellationToken).ConfigureAwait(false);
        return response.Status == BackendApiStatus.NotFound ? null : CloudStoreResponse.RequireData(response, "read planned workout session");
    }

    /// <summary>Starts the planned session, or returns the one already running so a second tap does not start another.</summary>
    public async Task<ActiveWorkoutSessionSnapshot> StartPlannedSessionAsync(int workoutDayId, Guid sessionId, DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        var request = new StartWorkoutSessionCommand(sessionId.ToString("N"), sessionId, "Workout", startedAtUtc, PlannedWorkoutDayId: workoutDayId);
        var response = await apiClient.SendMutationAsync<StartWorkoutSessionCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, "/api/v1/workouts/sessions/start", request, idempotencyKey: sessionId.ToString("N"), cancellationToken).ConfigureAwait(false);
        return RequirePlannedSession(response, "start planned workout session");
    }

    /// <summary>Finishes a session and returns what was recorded.</summary>
    public async Task<ActiveWorkoutSessionSnapshot> FinishPlannedSessionAsync(Guid sessionId, DateTime completedAtUtc, int totalDurationSeconds, int? rpeRating, decimal? distanceKm, CancellationToken cancellationToken)
    {
        var request = new FinishWorkoutSessionCommand(sessionId.ToString("N"), sessionId, completedAtUtc, totalDurationSeconds, rpeRating, null, DistanceKm: distanceKm);
        var response = await apiClient.SendMutationAsync<FinishWorkoutSessionCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, $"/api/v1/workouts/sessions/{sessionId:D}/finish", request, idempotencyKey: sessionId.ToString("N"), cancellationToken).ConfigureAwait(false);
        return RequirePlannedSession(response, "finish planned workout session");
    }

    /// <summary>Abandons a session without recording it as training.</summary>
    public async Task AbandonPlannedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var request = new CancelWorkoutSessionCommand(sessionId.ToString("N"), sessionId);
        var response = await apiClient.SendMutationAsync<CancelWorkoutSessionCommand, ActiveWorkoutSessionSnapshot>(
            HttpMethod.Post, $"/api/v1/workouts/sessions/{sessionId:D}/cancel", request, idempotencyKey: sessionId.ToString("N"), cancellationToken).ConfigureAwait(false);
        RequirePlannedSession(response, "abandon planned workout session");
    }

    /// <summary>Resumes the planned session's paused timing.</summary>
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

    /// <summary>Records progress of the planned session's timed interval.</summary>
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

    /// <summary>The planned session the backend returned, or a refusal.</summary>
    private static ActiveWorkoutSessionSnapshot RequirePlannedSession(
        BackendApiResponse<ActiveWorkoutSessionSnapshot> response, string operation)
    {
        if (response.Status == BackendApiStatus.Conflict &&
            response.ErrorCode == WorkoutHiitHistoryConfirmationRequiredException.ErrorCode)
            throw new WorkoutHiitHistoryConfirmationRequiredException();
        if (response.Status == BackendApiStatus.Conflict && response.ErrorCode == "WorkoutActivityNoLongerSafe")
            throw new WorkoutActivityNoLongerSafeException();
        if (response.Status == BackendApiStatus.Conflict && WorkoutSessionStartBlockCode.IsKnown(response.ErrorCode))
            throw new WorkoutSessionStartBlockedException(response.ErrorCode!);
        if (response.Status == BackendApiStatus.OfflineRejected)
            throw new HttpRequestException("Workout session requires a connection.");
        return CloudStoreResponse.RequireData(response, operation);
    }
    /// <summary>The detail of one workout day.</summary>
    public async Task<WorkoutSessionDetailDto> GetDetailAsync(int workoutDayId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutSessionDetailDto>(
            $"/api/v1/workouts/day-sessions/{workoutDayId}", cancellationToken).ConfigureAwait(false), "workout session detail");

    /// <summary>Logs a completed workout session.</summary>
    public async Task LogAsync(LogWorkoutSessionRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<LogWorkoutSessionRequest, object>(
            HttpMethod.Post, "/api/v1/workouts/day-sessions/log", request, cancellationToken: cancellationToken).ConfigureAwait(false), "log workout session");

    /// <summary>The swap candidates for one exercise.</summary>
    public async Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSwapCandidatesAsync(int workoutDayExerciseId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<WorkoutSwapCandidateDto>>(
            $"/api/v1/workouts/day-exercises/{workoutDayExerciseId}/swaps", cancellationToken).ConfigureAwait(false), "workout swap candidates");

    /// <summary>Swaps one exercise.</summary>
    public async Task SwapAsync(SwapWorkoutExerciseRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SwapWorkoutExerciseRequest, object>(
            HttpMethod.Post, "/api/v1/workouts/day-exercises/swap", request, cancellationToken: cancellationToken).ConfigureAwait(false), "swap workout exercise");

    /// <summary>The movement note of an exercise.</summary>
    public async Task<WorkoutMovementNoteDto> GetMovementNoteAsync(int workoutDayExerciseId, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutMovementNoteDto>(
            $"/api/v1/workouts/day-exercises/{workoutDayExerciseId}/movement-note", cancellationToken).ConfigureAwait(false), "workout movement note");

    /// <summary>Saves a movement note.</summary>
    public async Task SaveMovementNoteAsync(SaveWorkoutMovementNoteRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SaveWorkoutMovementNoteRequest, object>(
            HttpMethod.Put, "/api/v1/workouts/day-exercises/movement-note", request, cancellationToken: cancellationToken).ConfigureAwait(false), "save workout movement note");

    /// <summary>Marks the exercise optional or required.</summary>
    public async Task SetExerciseOptionalAsync(SetWorkoutExerciseOptionalRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SetWorkoutExerciseOptionalRequest, object>(
            HttpMethod.Put, "/api/v1/workouts/optional-exercise", request, cancellationToken: cancellationToken).ConfigureAwait(false), "mark optional workout exercise");

    /// <summary>Splits a superset.</summary>
    public async Task SplitSupersetAsync(SplitWorkoutSupersetRequest request, CancellationToken cancellationToken) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<SplitWorkoutSupersetRequest, object>(
            HttpMethod.Put, "/api/v1/workouts/day-sessions/superset-split", request, cancellationToken: cancellationToken)
            .ConfigureAwait(false), "split workout superset");
}

/// <summary>The workout archive read through the backend.</summary>
public sealed class CloudWorkoutArchiveStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutArchiveStore
{
    private const string HistoryRoute = "/api/v1/workouts/history";

    /// <summary>The workout history page.</summary>
    public async Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryAsync(int limit, int offset, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<WorkoutArchiveSummaryDto>>(
            $"{HistoryRoute}?limit={limit}&offset={offset}", ct).ConfigureAwait(false), "workout history");

    /// <summary>One archived session's detail.</summary>
    public async Task<WorkoutArchiveDetailDto> GetWorkoutSessionDetailAsync(Guid sessionId, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutArchiveDetailDto>(
            $"{HistoryRoute}/sessions/{sessionId:D}", ct).ConfigureAwait(false), "workout session detail");

    /// <summary>Updates a session's notes and effort.</summary>
    public async Task UpdateWorkoutSessionNotesAsync(Guid sessionId, string notes, int? rpe, CancellationToken ct = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<UpdateWorkoutSessionNotesRequest, object>(
            HttpMethod.Put, $"{HistoryRoute}/sessions/{sessionId:D}/notes", new UpdateWorkoutSessionNotesRequest(notes, rpe),
            cancellationToken: ct).ConfigureAwait(false), "update workout notes");

    /// <summary>The workout history for a month.</summary>
    public async Task<IReadOnlyList<WorkoutArchiveSummaryDto>> GetWorkoutHistoryByMonthAsync(int year, int month, int limit = 100, int offset = 0, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<WorkoutArchiveSummaryDto>>(
            $"{HistoryRoute}/month/{year}/{month}?limit={limit}&offset={offset}", ct).ConfigureAwait(false), "monthly workout history");

    /// <summary>Updates one archived exercise set.</summary>
    public async Task UpdateExerciseSetAsync(Guid setId, double weightKg, int repsCompleted, CancellationToken ct = default) =>
        CloudStoreResponse.RequireSuccess(await apiClient.SendMutationAsync<UpdateExerciseSetRequest, object>(
            HttpMethod.Put, $"{HistoryRoute}/sets/{setId:D}", new UpdateExerciseSetRequest(weightKg, repsCompleted),
            cancellationToken: ct).ConfigureAwait(false), "update workout set");

    /// <summary>Total sessions the user has finished.</summary>
    public async Task<int> GetTotalSessionsCountAsync(CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutHistoryCountDto>(
            $"{HistoryRoute}/count", ct).ConfigureAwait(false), "workout history count").Count;

    /// <summary>The months the user has history for.</summary>
    public async Task<IReadOnlyList<HistoryMonthOptionDto>> GetAvailableHistoryMonthsAsync(CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<List<HistoryMonthOptionDto>>(
            $"{HistoryRoute}/months", ct).ConfigureAwait(false), "workout history months");

    /// <summary>Sessions finished in a month.</summary>
    public async Task<int> GetWorkoutHistoryCountByMonthAsync(int year, int month, CancellationToken ct = default) =>
        CloudStoreResponse.RequireData(await apiClient.GetAsync<WorkoutHistoryCountDto>(
            $"{HistoryRoute}/month/{year}/{month}/count", ct).ConfigureAwait(false), "monthly workout count").Count;
}

/// <summary>Turns a refused backend response into an exception that names the operation, so a failure is never swallowed</summary>
/// as an empty result.</summary>
internal static class CloudStoreResponse
{
    /// <summary>The response data, or an exception naming the failed operation.</summary>
    public static T RequireData<T>(BackendApiResponse<T> response, string operation) where T : class
    {
        RequireSuccess(response, operation);
        return response.Data ?? throw new InvalidOperationException($"Cloud {operation} returned no data.");
    }

    /// <summary>Throws for a refused response, carrying the operation's name into the message.</summary>
    public static void RequireSuccess<T>(BackendApiResponse<T> response, string operation)
    {
        if (response.Status != BackendApiStatus.Success)
            throw new InvalidOperationException($"Cloud {operation} failed: {response.Status} ({response.ErrorCode ?? "unknown"}).");
    }
}
