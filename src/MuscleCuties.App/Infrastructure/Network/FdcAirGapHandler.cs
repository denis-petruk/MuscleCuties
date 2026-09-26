using System.Net.Http.Headers;
using System.Text;

namespace MuscleCuties.App.Infrastructure.Network;

public sealed class FdcAirGapHandler : DelegatingHandler
{
    private static readonly HashSet<string> AllowedQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_key",
        "dataType",
        "fdcIds",
        "format",
        "pageNumber",
        "pageSize",
        "query",
        "sortBy",
        "sortOrder"
    };

    private readonly IFdcApiKeyProvider _apiKeyProvider;

    public FdcAirGapHandler(IFdcApiKeyProvider apiKeyProvider)
    {
        _apiKeyProvider = apiKeyProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        StripUserSpecificHeaders(request);
        request.RequestUri = await BuildAirGappedUriAsync(request.RequestUri, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Uri?> BuildAirGappedUriAsync(Uri? uri, CancellationToken cancellationToken)
    {
        if (uri is null)
            return null;

        var builder = new UriBuilder(uri);
        var query = ParseQuery(builder.Query);
        foreach (var key in query.Keys.Where(key => !AllowedQueryKeys.Contains(key)).ToArray())
            query.Remove(key);

        if (query.TryGetValue("query", out var searchTerm))
        {
            var normalized = FdcApiClient.NormalizeGenericIngredientQuery(searchTerm);
            if (string.IsNullOrWhiteSpace(normalized))
                query.Remove("query");
            else
                query["query"] = normalized;
        }

        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(apiKey))
            query["api_key"] = apiKey;

        builder.Query = BuildQuery(query);
        return builder.Uri;
    }

    private static void StripUserSpecificHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = null;
        request.Headers.Remove("Cookie");
        request.Headers.Remove("X-Device-Id");
        request.Headers.Remove("X-User-Id");
        request.Headers.Remove("X-Session-Id");
        request.Headers.UserAgent.Clear();
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
            return values;

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                continue;

            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            values[key] = value;
        }

        return values;
    }

    private static string BuildQuery(IReadOnlyDictionary<string, string> values)
    {
        var builder = new StringBuilder();
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (builder.Length > 0)
                builder.Append('&');

            builder
                .Append(Uri.EscapeDataString(pair.Key))
                .Append('=')
                .Append(Uri.EscapeDataString(pair.Value));
        }

        return builder.ToString();
    }
}
