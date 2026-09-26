using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.App.Infrastructure.Network;

public sealed class FdcApiClient : IFdcApiClient
{
    private const int MaxRateLimitRetries = 1;
    private const int MaxServerErrorRetries = 1;
    private const int MaxQueryLength = 80;
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SensitiveQueryTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "amenorrhea",
        "bleeding",
        "bmi",
        "bodyweight",
        "cramp",
        "cramps",
        "cycle",
        "fertility",
        "follicular",
        "health",
        "hip",
        "hrv",
        "i",
        "im",
        "inch",
        "inches",
        "injured",
        "injury",
        "kg",
        "kilogram",
        "kilograms",
        "knee",
        "lb",
        "lbs",
        "luteal",
        "me",
        "medical",
        "medication",
        "meds",
        "menstrual",
        "menstruation",
        "mine",
        "my",
        "myself",
        "ovulation",
        "ovulatory",
        "pain",
        "period",
        "phase",
        "pms",
        "pound",
        "pounds",
        "pregnancy",
        "pregnant",
        "readiness",
        "recovery",
        "rehab",
        "sleep",
        "sore",
        "sprain",
        "strain",
        "symptom",
        "symptoms",
        "weigh",
        "weighing",
        "weighs",
        "weight",
        "workout"
    };

    public static readonly Uri BaseUri = new("https://api.nal.usda.gov/fdc/v1/");

    private readonly HttpClient _httpClient;
    private readonly ILogger<FdcApiClient>? _logger;

    public FdcApiClient(HttpClient httpClient, ILogger<FdcApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = BaseUri;
    }

    public async Task<IReadOnlyList<FdcFoodSearchResult>> SearchFoodsAsync(
        string query,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var safeQuery = NormalizeGenericIngredientQuery(query);
        if (string.IsNullOrWhiteSpace(safeQuery) || !CanSendRequest())
            return [];

        pageSize = Math.Clamp(pageSize, 1, 200);
        pageNumber = Math.Max(1, pageNumber);

        var path = BuildSearchPath(safeQuery, pageSize, pageNumber);

        var payload = await SendAndReadAsync<FdcFoodSearchResponse>(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken).ConfigureAwait(false);

        return payload?.Foods ?? [];
    }

    public async Task<FdcFoodDetail?> GetFoodAsync(int fdcId, CancellationToken cancellationToken = default)
    {
        if (fdcId <= 0 || !CanSendRequest())
            return null;

        var path = "food/{fdcId}?format=abridged".Replace("{fdcId}", fdcId.ToString());
        return await SendAndReadAsync<FdcFoodDetail>(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FdcFoodDetail>> GetFoodsAsync(
        IEnumerable<int> fdcIds,
        CancellationToken cancellationToken = default)
    {
        if (!CanSendRequest())
            return [];

        var ids = fdcIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        const string path = "foods";
        var payload = await SendAndReadAsync<List<FdcFoodDetail>>(
            () => new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new FdcFoodsRequest { FdcIds = ids }, options: JsonOptions)
            },
            cancellationToken).ConfigureAwait(false);

        return payload ?? [];
    }

    internal static string NormalizeGenericIngredientQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var normalized = new StringBuilder(query.Length);
        foreach (var character in query.Trim())
        {
            if (char.IsLetterOrDigit(character) ||
                character is ' ' or '-' or '\'' or ',' or '%' or '&')
            {
                normalized.Append(character);
            }
            else
            {
                normalized.Append(' ');
            }
        }

        var safeTerms = normalized
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => !term.Any(char.IsDigit))
            .Where(term => !SensitiveQueryTerms.Contains(term))
            .Take(8);

        var safeQuery = string.Join(' ', safeTerms).Trim();
        return safeQuery.Length <= MaxQueryLength
            ? safeQuery
            : safeQuery[..MaxQueryLength].Trim();
    }

    private static string BuildSearchPath(string safeQuery, int pageSize, int pageNumber)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["query"] = safeQuery,
            ["dataType"] = "Foundation,SR Legacy,Survey (FNDDS),Branded",
            ["pageSize"] = pageSize.ToString(),
            ["pageNumber"] = pageNumber.ToString(),
            ["sortBy"] = "score",
            ["sortOrder"] = "desc"
        };

        return string.Concat("foods", "/", "search", "?", BuildQuery(parameters));
    }

    private static string BuildQuery(IReadOnlyDictionary<string, string> parameters)
    {
        return string.Join(
            '&',
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
    }

    private async Task<T?> SendAndReadAsync<T>(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendWithRetryAsync(createRequest, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                return default;

            if (!response.IsSuccessStatusCode)
            {
                LogFailure("FoodData Central returned {StatusCode}.", response.StatusCode);
                return default;
            }

            return await response.Content
                .ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure("FoodData Central request timed out.");
            return default;
        }
        catch (HttpRequestException ex)
        {
            LogFailure(ex, "FoodData Central request failed.");
            return default;
        }
        catch (JsonException ex)
        {
            LogFailure(ex, "FoodData Central returned an unreadable payload.");
            return default;
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        var rateLimitRetries = 0;
        var serverErrorRetries = 0;

        while (true)
        {
            using var request = createRequest();
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests &&
                rateLimitRetries < MaxRateLimitRetries)
            {
                var delay = GetRateLimitDelay(response);
                response.Dispose();
                rateLimitRetries++;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if ((int)response.StatusCode >= 500 && serverErrorRetries < MaxServerErrorRetries)
            {
                response.Dispose();
                serverErrorRetries++;
                continue;
            }

            return response;
        }
    }

    private bool CanSendRequest()
    {
        if (!HasInternetAccess())
        {
            LogFailure("FoodData Central request skipped because the device is offline.");
            return false;
        }

        return true;
    }

    private static bool HasInternetAccess()
    {
        try
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
        catch (Exception ex) when (ex is FeatureNotEnabledException or FeatureNotSupportedException)
        {
            return true;
        }
    }

    private static TimeSpan GetRateLimitDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
            return ClampDelay(delta);

        if (retryAfter?.Date is { } date)
            return ClampDelay(date - DateTimeOffset.UtcNow);

        return DefaultRateLimitDelay;
    }

    private static TimeSpan ClampDelay(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            return DefaultRateLimitDelay;

        return delay > MaxRateLimitDelay ? MaxRateLimitDelay : delay;
    }

    private void LogFailure(string message, params object?[] args)
    {
        _logger?.LogInformation(message, args);
    }

    private void LogFailure(Exception exception, string message)
    {
        _logger?.LogInformation(exception, message);
    }
}
