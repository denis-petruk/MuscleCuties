using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public class FdcApiClient : IFdcApiClient
{
    public static readonly Uri BaseUri = new("https://api.nal.usda.gov/fdc/v1/");

    private const int MaxRateLimitRetries = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public FdcApiClient(HttpClient httpClient)
        : this(httpClient, Environment.GetEnvironmentVariable("FDC_API_KEY"))
    {
    }

    public FdcApiClient(HttpClient httpClient, string? apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = BaseUri;
    }

    public async Task<IReadOnlyList<FdcFoodSearchResult>> SearchFoodsAsync(
        string query,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        pageSize = Math.Clamp(pageSize, 1, 200);
        pageNumber = Math.Max(1, pageNumber);

        var path =
            $"foods/search?query={Uri.EscapeDataString(query.Trim())}" +
            $"&dataType={Uri.EscapeDataString("Foundation,SR Legacy,Survey (FNDDS),Branded")}" +
            $"&pageSize={pageSize}" +
            $"&pageNumber={pageNumber}" +
            $"&sortBy=score" +
            $"&sortOrder=desc" +
            $"&api_key={Uri.EscapeDataString(ApiKey)}";

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            return [];

        ThrowIfConfigurationError(response);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FdcFoodSearchResponse>(JsonOptions, cancellationToken);
        return payload?.Foods ?? [];
    }

    public async Task<FdcFoodDetail?> GetFoodAsync(int fdcId, CancellationToken cancellationToken = default)
    {
        if (fdcId <= 0)
            return null;

        var path = $"food/{fdcId}?format=abridged&api_key={Uri.EscapeDataString(ApiKey)}";
        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            return null;

        ThrowIfConfigurationError(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FdcFoodDetail>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<FdcFoodDetail>> GetFoodsAsync(
        IEnumerable<int> fdcIds,
        CancellationToken cancellationToken = default)
    {
        var ids = fdcIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var path = $"foods?api_key={Uri.EscapeDataString(ApiKey)}";
        using var response = await SendWithRetryAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, path)
                {
                    Content = JsonContent.Create(new FdcFoodsRequest { FdcIds = ids }, options: JsonOptions)
                };

                return request;
            },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            return [];

        ThrowIfConfigurationError(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<FdcFoodDetail>>(JsonOptions, cancellationToken) ?? [];
    }

    private string ApiKey =>
        string.IsNullOrWhiteSpace(_apiKey)
            ? throw new InvalidOperationException("FDC_API_KEY is not configured.")
            : _apiKey;

    private static void ThrowIfConfigurationError(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("FoodData Central rejected the configured FDC_API_KEY.");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        var rateLimitRetries = 0;
        var retriedServerError = false;

        while (true)
        {
            using var request = createRequest();
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && rateLimitRetries < MaxRateLimitRetries)
            {
                response.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                rateLimitRetries++;
                continue;
            }

            if ((int)response.StatusCode >= 500 && !retriedServerError)
            {
                response.Dispose();
                retriedServerError = true;
                continue;
            }

            return response;
        }
    }
}
