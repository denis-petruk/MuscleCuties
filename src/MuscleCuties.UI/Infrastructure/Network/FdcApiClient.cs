using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.UI.Infrastructure.Network;

public sealed partial class FdcApiClient : IFdcApiClient
{
    private const int MaxRateLimitRetries = 1;
    private const int MaxServerErrorRetries = 1;
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(2);
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
            FdcJsonContext.Default.FdcFoodSearchResponse,
            cancellationToken).ConfigureAwait(false);

        return payload?.Foods ?? [];
    }

    public Task<FdcFoodDetail?> GetFoodAsync(int fdcId, CancellationToken cancellationToken = default)
    {
        // Retain the interface for local callers; ID-based requests are outside
        // the app's generic-query-only network policy.
        return Task.FromResult<FdcFoodDetail?>(null);
    }

    public Task<IReadOnlyList<FdcFoodDetail>> GetFoodsAsync(
        IEnumerable<int> fdcIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<FdcFoodDetail>>([]);
    }

    internal static string NormalizeGenericIngredientQuery(string query)
    {
        return GenericFoodQueryPolicy.NormalizeOrEmpty(query);
    }

    private static string BuildSearchPath(string safeQuery, int pageSize, int pageNumber)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["query"] = safeQuery,
            ["dataType"] = "Foundation,SR Legacy,Survey (FNDDS),Branded",
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture),
            ["pageNumber"] = pageNumber.ToString(CultureInfo.InvariantCulture),
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
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendWithRetryAsync(createRequest, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                return default;

            if (!response.IsSuccessStatusCode)
            {
                if (_logger is not null)
                    LogNonSuccessStatus(_logger, response.StatusCode);
                return default;
            }

            return await response.Content
                .ReadFromJsonAsync(jsonTypeInfo, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (_logger is not null)
                LogRequestTimedOut(_logger);
            return default;
        }
        catch (HttpRequestException ex)
        {
            if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                LogRequestFailed(_logger, nameof(HttpRequestException), ex);
            return default;
        }
        catch (JsonException ex)
        {
            if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                LogUnreadablePayload(_logger, nameof(JsonException), ex);
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
                var delay = NetworkRetryHelper.GetRateLimitDelay(response, DefaultRateLimitDelay, MaxRateLimitDelay);
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
        if (!NetworkRetryHelper.HasInternetAccess())
        {
            if (_logger is not null)
                LogOfflineSkipped(_logger);
            return false;
        }

        return true;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "FoodData Central request skipped because the device is offline.")]
    private static partial void LogOfflineSkipped(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "FoodData Central returned {StatusCode}.")]
    private static partial void LogNonSuccessStatus(ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "FoodData Central request timed out.")]
    private static partial void LogRequestTimedOut(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "FoodData Central request failed. Failure type: {ExceptionType}.")]
    private static partial void LogRequestFailed(ILogger logger, string exceptionType, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "FoodData Central returned an unreadable payload. Failure type: {ExceptionType}.")]
    private static partial void LogUnreadablePayload(ILogger logger, string exceptionType, Exception ex);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(FdcFoodSearchResponse))]
[JsonSerializable(typeof(FdcFoodDetail))]
[JsonSerializable(typeof(List<FdcFoodDetail>))]
[JsonSerializable(typeof(FdcFoodsRequest))]
internal sealed partial class FdcJsonContext : JsonSerializerContext
{
}
