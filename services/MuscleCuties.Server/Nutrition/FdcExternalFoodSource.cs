using System.Globalization;
using System.Text.Json;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

namespace MuscleCuties.Server.Nutrition;

/// <summary>
///     USDA FoodData Central generic-food search. The key comes from server configuration
///     (<c>FDC_API_KEY</c>) and travels in a request header, never in the URL. Only the generic query text is
///     sent; any failure yields no results so the catalog search itself is unaffected.
/// </summary>
public sealed class FdcExternalFoodSource : IExternalFoodSource
{
    public static readonly Uri BaseUri = new("https://api.nal.usda.gov/fdc/v1/");

    private const string ApiKeyHeader = "X-Api-Key";
    private const string GenericDataTypes = "Foundation,SR Legacy,Survey (FNDDS)";
    private const int PageSize = 15;
    private const int EnergyKcal = 1008;
    private const int EnergyAtwaterGeneral = 2047;
    private const int EnergyAtwaterSpecific = 2048;
    private const int Protein = 1003;
    private const int Fat = 1004;
    private const int Carbohydrate = 1005;
    private const int MaximumNameLength = 200;

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ILogger<FdcExternalFoodSource>? _logger;

    public FdcExternalFoodSource(HttpClient httpClient, string? apiKey, ILogger<FdcExternalFoodSource>? logger = null)
    {
        _httpClient = httpClient;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _logger = logger;
        _httpClient.BaseAddress ??= BaseUri;
    }

    public bool IsConfigured => _apiKey is not null;

    public async Task<IReadOnlyList<ExternalFoodItem>> SearchAsync(
        string genericQuery,
        CancellationToken cancellationToken = default)
    {
        if (_apiKey is null || string.IsNullOrWhiteSpace(genericQuery))
            return [];

        var path = string.Create(CultureInfo.InvariantCulture,
            $"foods/search?query={Uri.EscapeDataString(genericQuery)}&dataType={Uri.EscapeDataString(GenericDataTypes)}&pageSize={PageSize}&pageNumber=1");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation(ApiKeyHeader, _apiKey);
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("External food search returned status {StatusCode}.", (int)response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return Parse(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning("External food search timed out.");
            return [];
        }
        catch (HttpRequestException exception)
        {
            _logger?.LogWarning("External food search failed ({ErrorType}).", exception.GetType().Name);
            return [];
        }
        catch (JsonException)
        {
            _logger?.LogWarning("External food search returned an unreadable payload.");
            return [];
        }
    }

    /// <summary>Keeps only foods with a name and all four macronutrients; nothing is estimated.</summary>
    public static IReadOnlyList<ExternalFoodItem> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("foods", out var foods) || foods.ValueKind != JsonValueKind.Array)
            return [];

        var items = new List<ExternalFoodItem>();
        foreach (var food in foods.EnumerateArray())
        {
            if (food.ValueKind != JsonValueKind.Object ||
                !food.TryGetProperty("fdcId", out var idElement) || !idElement.TryGetInt32(out var fdcId) || fdcId <= 0 ||
                !food.TryGetProperty("description", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                continue;

            var name = nameElement.GetString()?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MaximumNameLength)
                continue;

            var nutrients = ReadNutrients(food);
            var calories = nutrients.GetValueOrDefault(EnergyKcal)
                           ?? nutrients.GetValueOrDefault(EnergyAtwaterSpecific)
                           ?? nutrients.GetValueOrDefault(EnergyAtwaterGeneral);
            if (calories is null ||
                nutrients.GetValueOrDefault(Protein) is not { } protein ||
                nutrients.GetValueOrDefault(Carbohydrate) is not { } carbs ||
                nutrients.GetValueOrDefault(Fat) is not { } fats)
                continue;

            var dataType = food.TryGetProperty("dataType", out var typeElement) &&
                           typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null;
            items.Add(new ExternalFoodItem(fdcId, name, calories.Value, protein, carbs, fats, dataType));
        }

        return items;
    }

    private static Dictionary<int, float?> ReadNutrients(JsonElement food)
    {
        var values = new Dictionary<int, float?>();
        if (!food.TryGetProperty("foodNutrients", out var nutrients) || nutrients.ValueKind != JsonValueKind.Array)
            return values;

        foreach (var nutrient in nutrients.EnumerateArray())
        {
            if (nutrient.ValueKind != JsonValueKind.Object ||
                !nutrient.TryGetProperty("nutrientId", out var idElement) || !idElement.TryGetInt32(out var id) ||
                !nutrient.TryGetProperty("value", out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.Number || !valueElement.TryGetSingle(out var value) ||
                !float.IsFinite(value) || value < 0f)
                continue;

            values.TryAdd(id, value);
        }

        return values;
    }
}
