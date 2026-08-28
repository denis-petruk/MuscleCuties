using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.App.Services.Health;

public sealed class WhoopDataProvider : IHealthDataProvider, IHealthDataProviderDiagnostics
{
    private const string TokenStorageKey = "whoop_oauth_token_set";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly ITokenStorage _tokenStorage;
    private readonly WhoopOAuthOptions _options;

    public WhoopDataProvider(ITokenStorage tokenStorage, WhoopOAuthOptions options)
    {
        _tokenStorage = tokenStorage;
        _options = options;
    }

    public HealthDataSource Source => HealthDataSource.Whoop;
    public string DisplayName => "Whoop";
    public string UnavailableMessage =>
        "Whoop needs a developer Client ID plus a secure token exchange endpoint before it can connect.";
    public string EmptyDataMessage =>
        "Whoop connected, but it has not returned recovery or sleep data for this week yet.";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
#if IOS || ANDROID || MACCATALYST
        return Task.FromResult(_options.IsConfigured);
#else
        return Task.FromResult(false);
#endif
    }

    public async Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            return null;

        var tokenSet = await GetUsableTokenSetAsync(cancellationToken);
        if (tokenSet is null)
            return null;

        var weekEnd = today.Date.AddDays(1);
        var weekStart = weekEnd.AddDays(-7);
        var recovery = await ReadRecoveryMetricsAsync(tokenSet.AccessToken, weekStart, weekEnd, cancellationToken);
        var sleep = await ReadSleepMetricsAsync(tokenSet.AccessToken, weekStart, weekEnd, cancellationToken);

        if (!recovery.HasData && !sleep.HasData)
            return null;

        var sleepQualityScore = sleep.SleepQualityScore > 0
            ? sleep.SleepQualityScore
            : EstimateSleepScore(sleep.AverageSleepHours);

        return new HealthWeeklySummary(
            Source,
            weekStart,
            weekEnd.AddDays(-1),
            0,
            sleep.AverageSleepHours,
            sleepQualityScore,
            recovery.RestingHeartRate,
            recovery.HrvScore,
            DateTime.UtcNow);
    }

    private async Task<WhoopTokenSet?> GetUsableTokenSetAsync(CancellationToken cancellationToken)
    {
        var stored = await ReadStoredTokenSetAsync();
        if (stored is not null && stored.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
            return stored;

        if (!string.IsNullOrWhiteSpace(stored?.RefreshToken))
        {
            var refreshed = await RequestTokenAsync(
                "refresh_token",
                code: null,
                refreshToken: stored.RefreshToken,
                existingRefreshToken: stored.RefreshToken,
                cancellationToken);

            if (refreshed is not null)
                return refreshed;
        }

        return await AuthenticateAsync(cancellationToken);
    }

    private async Task<WhoopTokenSet?> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var state = Guid.NewGuid().ToString("N");
        var authUri = BuildAuthUri(state);

        WebAuthenticatorResult result;
        try
        {
            result = await MainThread.InvokeOnMainThreadAsync(
                () => WebAuthenticator.Default.AuthenticateAsync(authUri, _options.RedirectUri));
        }
        catch
        {
            return null;
        }

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            return null;

        if (result.Properties.TryGetValue("state", out var returnedState)
            && !string.Equals(returnedState, state, StringComparison.Ordinal))
            return null;

        return await RequestTokenAsync(
            "authorization_code",
            code,
            refreshToken: null,
            existingRefreshToken: null,
            cancellationToken);
    }

    private async Task<WhoopTokenSet?> RequestTokenAsync(
        string grantType,
        string? code,
        string? refreshToken,
        string? existingRefreshToken,
        CancellationToken cancellationToken)
    {
        var response = _options.TokenExchangeEndpoint is null
            ? await RequestTokenDirectlyAsync(grantType, code, refreshToken, cancellationToken)
            : await RequestTokenThroughBackendAsync(grantType, code, refreshToken, cancellationToken);

        if (response is null || string.IsNullOrWhiteSpace(response.AccessToken))
            return null;

        var tokenSet = new WhoopTokenSet(
            response.AccessToken,
            string.IsNullOrWhiteSpace(response.RefreshToken) ? existingRefreshToken ?? string.Empty : response.RefreshToken,
            DateTime.UtcNow.AddSeconds(Math.Max(300, response.ExpiresIn)));

        await _tokenStorage.SetAsync(TokenStorageKey, JsonSerializer.Serialize(tokenSet, JsonOptions));
        return tokenSet;
    }

    private async Task<WhoopTokenResponse?> RequestTokenDirectlyAsync(
        string grantType,
        string? code,
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["grant_type"] = grantType,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        };

        if (grantType == "authorization_code" && !string.IsNullOrWhiteSpace(code))
        {
            values["code"] = code;
            values["redirect_uri"] = _options.RedirectUri.ToString();
        }
        else if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            values["refresh_token"] = refreshToken;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(values)
        };

        return await SendTokenRequestAsync(request, cancellationToken);
    }

    private async Task<WhoopTokenResponse?> RequestTokenThroughBackendAsync(
        string grantType,
        string? code,
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenExchangeEndpoint)
        {
            Content = JsonContent.Create(new
            {
                provider = "whoop",
                grant_type = grantType,
                client_id = _options.ClientId,
                code,
                refresh_token = refreshToken,
                redirect_uri = _options.RedirectUri.ToString()
            })
        };

        return await SendTokenRequestAsync(request, cancellationToken);
    }

    private static async Task<WhoopTokenResponse?> SendTokenRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<WhoopTokenResponse>(stream, JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<WhoopRecoveryMetrics> ReadRecoveryMetricsAsync(
        string accessToken,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            accessToken,
            $"developer/v2/recovery{BuildDateRangeQuery(start, end, limit: 25)}",
            cancellationToken);

        if (document is null)
            return WhoopRecoveryMetrics.Empty;

        var recoveryScores = new List<double>();
        var restingHeartRates = new List<double>();
        var hrvScores = new List<double>();

        foreach (var record in EnumerateRecords(document.RootElement))
        {
            if (TryGetNestedDouble(record, out var recoveryScore, "score", "recovery_score"))
                recoveryScores.Add(recoveryScore);

            if (TryGetNestedDouble(record, out var restingHeartRate, "score", "resting_heart_rate"))
                restingHeartRates.Add(restingHeartRate);

            if (TryGetNestedDouble(record, out var hrvScore, "score", "hrv_rmssd_milli"))
                hrvScores.Add(hrvScore);
        }

        return new WhoopRecoveryMetrics(
            AverageAsInt(recoveryScores),
            AverageAsInt(restingHeartRates),
            AverageAsInt(hrvScores));
    }

    private async Task<WhoopSleepMetrics> ReadSleepMetricsAsync(
        string accessToken,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            accessToken,
            $"developer/v2/activity/sleep{BuildDateRangeQuery(start, end, limit: 25)}",
            cancellationToken);

        if (document is null)
            return WhoopSleepMetrics.Empty;

        var sleepHours = new List<double>();
        var sleepScores = new List<double>();

        foreach (var record in EnumerateRecords(document.RootElement))
        {
            if (TryGetNestedDouble(record, out var totalSleepMillis, "score", "stage_summary", "total_sleep_time_milli")
                || TryGetNestedDouble(record, out totalSleepMillis, "score", "stage_summary", "total_in_bed_time_milli"))
            {
                sleepHours.Add(totalSleepMillis / 1000d / 60d / 60d);
            }

            if (TryGetNestedDouble(record, out var performance, "score", "sleep_performance_percentage"))
                sleepScores.Add(performance);
        }

        return new WhoopSleepMetrics(
            Math.Round(Average(sleepHours), 1),
            AverageAsInt(sleepScores));
    }

    private async Task<JsonDocument?> GetJsonAsync(
        string accessToken,
        string relativeUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_options.ApiBaseUri, relativeUri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await Client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _tokenStorage.Remove(TokenStorageKey);
                return null;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private Uri BuildAuthUri(string state)
    {
        var query = BuildQuery(
            ("response_type", "code"),
            ("client_id", _options.ClientId),
            ("redirect_uri", _options.RedirectUri.ToString()),
            ("scope", _options.Scope),
            ("state", state));

        return new Uri($"{_options.AuthorizationEndpoint}?{query}");
    }

    private async Task<WhoopTokenSet?> ReadStoredTokenSetAsync()
    {
        var json = await _tokenStorage.GetAsync(TokenStorageKey);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<WhoopTokenSet>(json, JsonOptions);
        }
        catch (JsonException)
        {
            _tokenStorage.Remove(TokenStorageKey);
            return null;
        }
    }

    private static string BuildDateRangeQuery(DateTime start, DateTime end, int limit) =>
        "?" + BuildQuery(
            ("start", start.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            ("end", end.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            ("limit", limit.ToString(CultureInfo.InvariantCulture)));

    private static string BuildQuery(params (string Key, string Value)[] values) =>
        string.Join(
            "&",
            values.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));

    private static IEnumerable<JsonElement> EnumerateRecords(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                yield return item;

            yield break;
        }

        if (element.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in records.EnumerateArray())
                yield return item;

            yield break;
        }

        yield return element;
    }

    private static bool TryGetNestedDouble(JsonElement element, out double value, params string[] path)
    {
        value = 0d;
        var current = element;

        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return false;
        }

        if (current.ValueKind == JsonValueKind.Number)
            return current.TryGetDouble(out value);

        if (current.ValueKind == JsonValueKind.String)
            return double.TryParse(current.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        return false;
    }

    private static double Average(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? 0d : values.Average();

    private static int AverageAsInt(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? 0 : (int)Math.Round(values.Average());

    private static int EstimateSleepScore(double averageSleepHours) => averageSleepHours switch
    {
        >= 8.0 => 92,
        >= 7.0 => 84,
        >= 6.0 => 72,
        >= 5.0 => 58,
        > 0 => 45,
        _ => 0
    };

    private sealed record WhoopTokenSet(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAtUtc);

    private sealed record WhoopTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record WhoopRecoveryMetrics(
        int RecoveryScore,
        int RestingHeartRate,
        int HrvScore)
    {
        public static WhoopRecoveryMetrics Empty { get; } = new(0, 0, 0);
        public bool HasData => RecoveryScore > 0 || RestingHeartRate > 0 || HrvScore > 0;
    }

    private sealed record WhoopSleepMetrics(
        double AverageSleepHours,
        int SleepQualityScore)
    {
        public static WhoopSleepMetrics Empty { get; } = new(0d, 0);
        public bool HasData => AverageSleepHours > 0 || SleepQualityScore > 0;
    }
}
