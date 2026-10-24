using System.Net.Http.Headers;
using System.Text;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.UI.Infrastructure.Network;

public sealed class FdcAirGapHandler : DelegatingHandler
{
    private const string SearchPath = "/fdc/v1/foods/search";
    private const string DataTypes = "Foundation,SR Legacy,Survey (FNDDS),Branded";

    private static readonly HashSet<string> AllowedQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "dataType",
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
        if (request.Method != HttpMethod.Get || request.Content is not null ||
            request.RequestUri is null ||
            request.RequestUri.Scheme != Uri.UriSchemeHttps ||
            !request.RequestUri.IsDefaultPort ||
            !string.Equals(request.RequestUri.Host, "api.nal.usda.gov", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.RequestUri.AbsolutePath, SearchPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only generic food-name searches may use the external food service.");

        StripUserSpecificHeaders(request);
        request.RequestUri = await BuildAirGappedUriAsync(request.RequestUri, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Uri?> BuildAirGappedUriAsync(Uri? uri, CancellationToken cancellationToken)
    {
        if (uri is null)
            return null;

        var builder = new UriBuilder(uri);
        builder.UserName = string.Empty;
        builder.Password = string.Empty;
        builder.Fragment = string.Empty;
        var query = ParseQuery(builder.Query);
        foreach (var key in query.Keys.Where(key => !AllowedQueryKeys.Contains(key)).ToArray())
            query.Remove(key);

        if (query.TryGetValue("query", out var searchTerm))
        {
            var normalized = GenericFoodQueryPolicy.NormalizeOrEmpty(searchTerm);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("The external food query is not a recognized generic food name.");

            query["query"] = normalized;
        }
        else
        {
            throw new InvalidOperationException("The external food search requires a recognized generic food name.");
        }

        if (query.TryGetValue("dataType", out var dataType) &&
            !string.Equals(dataType, DataTypes, StringComparison.Ordinal))
            throw new InvalidOperationException("The external food search contains unsupported filters.");
        if ((query.TryGetValue("sortBy", out var sortBy) && sortBy != "score") ||
            (query.TryGetValue("sortOrder", out var sortOrder) && sortOrder != "desc"))
            throw new InvalidOperationException("The external food search contains unsupported sorting.");
        foreach (var key in new[] { "pageNumber", "pageSize" })
        {
            if (!query.TryGetValue(key, out var value))
                continue;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
                number < 1 || (key == "pageSize" && number > 200))
                throw new InvalidOperationException("The external food search contains invalid pagination.");
            query[key] = number.ToString(CultureInfo.InvariantCulture);
        }

        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(apiKey))
            query["api_key"] = apiKey;

        builder.Query = BuildQuery(query);
        return builder.Uri;
    }

    private static void StripUserSpecificHeaders(HttpRequestMessage request)
    {
        request.Headers.Clear();
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
