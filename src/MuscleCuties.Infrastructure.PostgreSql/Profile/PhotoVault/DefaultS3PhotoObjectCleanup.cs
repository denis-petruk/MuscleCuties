using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>Signed, bounded S3 version-list and exact-version delete adapter.</summary>
public sealed class DefaultS3PhotoObjectCleanup : IS3PhotoObjectCleanup
{
    private const string DefaultBucket = "musclecuties-media";
    private const string DefaultRegion = "us-east-1";
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const int MaxAttempts = 3;
    private const int MaxPages = 100;
    private const int MaxSweepPasses = 3;
    private const int MaxResponseBytes = 1_048_576;
    private const int PageSize = 1000;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private const int RetryDelayMilliseconds = 100;

    private readonly HttpClient _httpClient;
    private readonly string _bucket;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string? _sessionToken;
    private readonly TimeProvider _timeProvider;

    public DefaultS3PhotoObjectCleanup(HttpClient httpClient, IConfiguration configuration)
        : this(httpClient, configuration, TimeProvider.System) { }

    internal DefaultS3PhotoObjectCleanup(HttpClient httpClient, IConfiguration configuration, TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _bucket = configuration["Storage:S3:BucketName"] ?? DefaultBucket;
        _region = configuration["Storage:S3:Region"] ?? DefaultRegion;
        _accessKey = ReadCredential(configuration, "Storage:S3:AccessKeyId", "AWS_ACCESS_KEY_ID");
        _secretKey = ReadCredential(configuration, "Storage:S3:SecretAccessKey", "AWS_SECRET_ACCESS_KEY");
        _sessionToken = configuration["Storage:S3:SessionToken"] ?? configuration["AWS_SESSION_TOKEN"];
        if (_bucket.Length == 0 || _region.Length == 0) throw ConfigurationFailure();
    }

    public async Task DeleteAllVersionsAsync(PhotoObjectCleanupTarget target, CancellationToken cancellationToken = default)
    {
        Validate(target);
        // One durable owner/photo target covers both namespaces, including a publication
        // whose metadata transaction failed after S3 accepted its conditional PUT.
        await DeleteKeyVersionsAsync(PhotoObjectKeys.Upload(target.OwnerId, target.PhotoId), cancellationToken).ConfigureAwait(false);
        await DeleteKeyVersionsAsync(PhotoObjectKeys.Published(target.OwnerId, target.PhotoId), cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteKeyVersionsAsync(string key, CancellationToken cancellationToken)
    {
        for (var page = 0; page < MaxSweepPasses; page++)
        {
            string? keyMarker = null;
            string? versionMarker = null;
            var foundAny = false;
            for (var listingPage = 0; listingPage < MaxPages; listingPage++)
            {
                var result = await ListPageWithRetryAsync(key, keyMarker, versionMarker, cancellationToken).ConfigureAwait(false);
                foundAny |= result.Versions.Count > 0 || result.DeleteMarkers.Count > 0;
                foreach (var version in result.Versions.Concat(result.DeleteMarkers))
                {
                    if (!string.Equals(version.Key, key, StringComparison.Ordinal) || string.IsNullOrEmpty(version.VersionId))
                        throw ConfigurationFailure();
                    await DeleteVersionWithRetryAsync(key, version.VersionId, cancellationToken).ConfigureAwait(false);
                }
                if (!result.IsTruncated)
                {
                    if (!foundAny) return;
                    break;
                }
                if (!string.Equals(result.NextKeyMarker, key, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(result.NextVersionIdMarker))
                    throw ConfigurationFailure();
                keyMarker = result.NextKeyMarker;
                versionMarker = result.NextVersionIdMarker;
            }
            // Repeat from page one after the full paginated pass. This catches writes that raced the
            // listing and guarantees completion only after S3 reports no versions or markers.
        }
        throw TransientFailure();
    }

    private async Task<ListResult> ListPageWithRetryAsync(string key, string? keyMarker, string? versionMarker, CancellationToken token)
    {
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["encoding-type"] = "url", ["key-marker"] = keyMarker ?? "", ["max-keys"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["prefix"] = key, ["versions"] = ""
        };
        if (versionMarker is not null) query["version-id-marker"] = versionMarker;
        using var response = await SendWithRetryAsync(HttpMethod.Get, key, query, null, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw Classify(response.StatusCode);
        using var bodyTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        bodyTimeout.CancelAfter(RequestTimeout);
        byte[] body;
        try { body = await ReadBoundedAsync(response, bodyTimeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested) { throw TransientFailure(exception); }
        catch (IOException exception) { throw TransientFailure(exception); }
        try { return ParseList(body, key); }
        catch (Exception exception) when (exception is XmlException or FormatException or InvalidOperationException)
        { throw ConfigurationFailure(exception); }
    }

    private async Task DeleteVersionWithRetryAsync(string key, string versionId, CancellationToken token)
    {
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["versionId"] = versionId };
        using var response = await SendWithRetryAsync(HttpMethod.Delete, key, query, null, token).ConfigureAwait(false);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return;
        throw Classify(response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpMethod method, string key, SortedDictionary<string, string> query, int? attemptOverride, CancellationToken token)
    {
        for (var attempt = 1; attempt <= (attemptOverride ?? MaxAttempts); attempt++)
        {
            using var request = CreateRequest(method, key, query);
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            requestTimeout.CancelAfter(RequestTimeout);
            try
            {
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestTimeout.Token).ConfigureAwait(false);
                if (!IsRetryable(response.StatusCode) || attempt == MaxAttempts) return response;
                response.Dispose();
            }
            catch (HttpRequestException exception)
            {
                if (attempt == MaxAttempts) throw TransientFailure(exception);
            }
            catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
            {
                if (attempt == MaxAttempts) throw TransientFailure(exception);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(RetryDelayMilliseconds * attempt), token).ConfigureAwait(false);
        }
        throw TransientFailure();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string key, SortedDictionary<string, string> query)
    {
        var uri = BuildUri(key, query);
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;
        var amzDate = timestamp.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = timestamp.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", EmptyPayloadHash);
        if (!string.IsNullOrEmpty(_sessionToken)) request.Headers.TryAddWithoutValidation("x-amz-security-token", _sessionToken);
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = uri.Host, ["x-amz-content-sha256"] = EmptyPayloadHash, ["x-amz-date"] = amzDate
        };
        if (!string.IsNullOrEmpty(_sessionToken)) headers["x-amz-security-token"] = _sessionToken;
        var signed = string.Join(';', headers.Keys);
        var canonicalHeaders = string.Concat(headers.Select(item => $"{item.Key}:{item.Value.Trim()}\n"));
        var canonicalQuery = string.Join('&', query.Select(item => $"{Encode(item.Key)}={Encode(item.Value)}"));
        var canonical = $"{method.Method}\n{uri.AbsolutePath}\n{canonicalQuery}\n{canonicalHeaders}\n{signed}\n{EmptyPayloadHash}";
        var scope = $"{dateStamp}/{_region}/s3/aws4_request";
        var toSign = $"{Algorithm}\n{amzDate}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))}";
        var dateKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("AWS4" + _secretKey), Encoding.UTF8.GetBytes(dateStamp));
        var regionKey = HMACSHA256.HashData(dateKey, Encoding.UTF8.GetBytes(_region));
        var serviceKey = HMACSHA256.HashData(regionKey, Encoding.UTF8.GetBytes("s3"));
        var signingKey = HMACSHA256.HashData(serviceKey, Encoding.UTF8.GetBytes("aws4_request"));
        var signature = Hex(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(toSign)));
        request.Headers.TryAddWithoutValidation("Authorization", $"{Algorithm} Credential={_accessKey}/{scope}, SignedHeaders={signed}, Signature={signature}");
        return request;
    }

    private Uri BuildUri(string key, SortedDictionary<string, string> query)
    {
        var path = string.Join('/', key.Split('/').Select(Encode));
        var queryString = string.Join('&', query.Select(item => $"{Encode(item.Key)}={Encode(item.Value)}"));
        return new Uri($"https://{_bucket}.s3.{_region}.amazonaws.com/{path}?{queryString}", UriKind.Absolute);
    }

    private void Validate(PhotoObjectCleanupTarget target)
    {
        if (target is null || target.OwnerId == Guid.Empty || target.PhotoId == Guid.Empty) throw ConfigurationFailure();
        if (!string.Equals(target.Bucket, _bucket, StringComparison.Ordinal)) throw ConfigurationFailure();
        if (target.Key != PhotoObjectKeys.Upload(target.OwnerId, target.PhotoId) &&
            target.Key != PhotoObjectKeys.Published(target.OwnerId, target.PhotoId)) throw ConfigurationFailure();
    }

    private static ListResult ParseList(byte[] body, string expectedKey)
    {
        using var stream = new MemoryStream(body, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxResponseBytes });
        var root = XDocument.Load(reader).Root ?? throw new XmlException();
        XNamespace ns = "http://s3.amazonaws.com/doc/2006-03-01/";
        var isTruncated = (string?)root.Element(ns + "IsTruncated");
        if (isTruncated is not ("true" or "false")) throw new XmlException();
        var versions = root.Elements(ns + "Version").Select(node => ReadListed(node, ns)).ToList();
        var markers = root.Elements(ns + "DeleteMarker").Select(node => ReadListed(node, ns)).ToList();
        if (versions.Any(item => item.Key != expectedKey) || markers.Any(item => item.Key != expectedKey)) throw new XmlException();
        if (isTruncated == "true")
        {
            // Both markers are required to continue a version listing safely.
            var nextKeyMarker = root.Element(ns + "NextKeyMarker") is { } keyMarker
                ? Uri.UnescapeDataString(keyMarker.Value)
                : null;
            var nextVersionIdMarker = root.Element(ns + "NextVersionIdMarker") is { } versionIdMarker
                ? Uri.UnescapeDataString(versionIdMarker.Value)
                : null;
            if (!string.Equals(nextKeyMarker, expectedKey, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(nextVersionIdMarker))
                throw new XmlException();
        }
        return new ListResult(versions, markers, isTruncated == "true",
            root.Element(ns + "NextKeyMarker") is { } nextKey ? Uri.UnescapeDataString(nextKey.Value) : null,
            root.Element(ns + "NextVersionIdMarker") is { } nextVersion ? Uri.UnescapeDataString(nextVersion.Value) : null);
    }

    private static ListedVersion ReadListed(XElement node, XNamespace ns)
    {
        var key = (string?)node.Element(ns + "Key") ?? throw new XmlException();
        var version = (string?)node.Element(ns + "VersionId") ?? throw new XmlException();
        return new ListedVersion(Uri.UnescapeDataString(key), Uri.UnescapeDataString(version));
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes) throw ConfigurationFailure();
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) return output.ToArray();
            if (output.Length + read > MaxResponseBytes) throw ConfigurationFailure();
            await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
        }
    }

    private static string ReadCredential(IConfiguration configuration, string key, string environmentKey)
    {
        var value = configuration[key] ?? configuration[environmentKey];
        return string.IsNullOrWhiteSpace(value) ? throw ConfigurationFailure() : value;
    }

    private static bool IsRetryable(HttpStatusCode status) => (int)status >= 500 || status == HttpStatusCode.RequestTimeout || (int)status == 429;
    private static S3PhotoObjectCleanupException Classify(HttpStatusCode status) =>
        status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
            ? new(S3PhotoObjectCleanupFailure.Forbidden, "S3 denied photo cleanup access.")
            : IsRetryable(status) ? TransientFailure() : ConfigurationFailure();
    private static S3PhotoObjectCleanupException ConfigurationFailure(Exception? inner = null) => new(S3PhotoObjectCleanupFailure.Configuration, "S3 photo cleanup configuration or response was invalid.", inner);
    private static S3PhotoObjectCleanupException TransientFailure(Exception? inner = null) => new(S3PhotoObjectCleanupFailure.Transient, "S3 photo cleanup request failed transiently.", inner);
    private static string Encode(string value) => Uri.EscapeDataString(value).Replace("%7E", "~", StringComparison.Ordinal);
    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();
    private sealed record ListedVersion(string Key, string VersionId);
    private sealed record ListResult(List<ListedVersion> Versions, List<ListedVersion> DeleteMarkers, bool IsTruncated, string? NextKeyMarker, string? NextVersionIdMarker);
}
