using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed partial class DefaultS3PhotoObjectInspector
{
    private const int MaximumTransportBytes = 15 * 1024 * 1024;
    private const int TransportBufferBytes = 81920;
    private static readonly TimeSpan PhotoTransportTimeout = TimeSpan.FromSeconds(30);

    public async Task<ReadOnlyMemory<byte>> ReadVersionAsync(PhotoUploadObjectContext context, string versionId,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        RequireVersion(versionId);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PhotoTransportTimeout);
        using var request = CreatePhotoRequest(HttpMethod.Get, context.Key, versionId);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            RequireSuccess(response);
            if (GetHeader(response.Headers, "x-amz-version-id") != versionId ||
                GetHeader(response.Headers, IntentMetadataHeader) != context.PhotoId.ToString("D") ||
                response.Content.Headers.ContentLength != context.DeclaredSizeBytes ||
                !string.Equals(response.Content.Headers.ContentType?.ToString(), context.ExpectedContentType, StringComparison.OrdinalIgnoreCase))
                throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "The exact-version photo response did not match its intent.");
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[TransportBufferBytes];
            int count;
            while ((count = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
            {
                if (output.Length + count > context.DeclaredSizeBytes || output.Length + count > MaximumTransportBytes)
                    throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "The photo response exceeded its declared bounds.");
                await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token).ConfigureAwait(false);
            }
            var bytes = output.ToArray();
            if (bytes.LongLength != context.DeclaredSizeBytes ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(context.ExpectedSha256ChecksumHex)))
                throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "The photo bytes did not match their upload checksum.");
            return bytes;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        { throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.Transient, "Photo transport timed out.", exception); }
        catch (HttpRequestException exception)
        { throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.Transient, "Photo transport failed.", exception); }
        catch (IOException exception)
        { throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.Transient, "Photo body transport failed.", exception); }
    }

    public async Task<S3PhotoPublication> PublishAsync(PhotoUploadObjectContext context, SanitizedPhotoBytes photo,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(photo);
        var bytes = photo.Bytes;
        if (bytes.Length is <= 0 or > MaximumTransportBytes || photo.ContentType != "image/jpeg")
            throw new ArgumentException("Only bounded sanitized JPEG output can be published.", nameof(photo));
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (checksum != photo.Sha256) throw new InvalidOperationException("Sanitized photo checksum mismatch.");
        var key = PhotoObjectKeys.Published(context.OwnerId, context.PhotoId);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PhotoTransportTimeout);
        using var request = CreatePhotoRequest(HttpMethod.Put, key, null, bytes, context.PhotoId);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // A lost PUT acknowledgement or transaction retry may find the immutable object.
                // Never replace it: confirm the exact content and intent before accepting replay.
                using var head = CreatePhotoRequest(HttpMethod.Head, key);
                using var existing = await _httpClient.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                RequireSuccess(existing);
                if (existing.Content.Headers.ContentLength != bytes.LongLength ||
                    existing.Content.Headers.ContentType?.ToString() != photo.ContentType ||
                    GetHeader(existing.Headers, IntentMetadataHeader) != context.PhotoId.ToString("D") ||
                    GetHeader(existing.Headers, "x-amz-checksum-sha256") != Convert.ToBase64String(SHA256.HashData(bytes)))
                    throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "An immutable photo publication conflicts with this intent.");
                var existingVersion = GetHeader(existing.Headers, "x-amz-version-id");
                RequireVersion(existingVersion);
                return new(key, existingVersion!, checksum);
            }
            RequireSuccess(response);
            var version = GetHeader(response.Headers, "x-amz-version-id");
            RequireVersion(version);
            if (GetHeader(response.Headers, "x-amz-checksum-sha256") != Convert.ToBase64String(SHA256.HashData(bytes)))
                throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "S3 did not confirm the sanitized publication checksum.");
            return new(key, version!, checksum);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        { throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.Transient, "Photo publication timed out.", exception); }
        catch (HttpRequestException exception)
        { throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.Transient, "Photo publication transport failed.", exception); }
    }

    private HttpRequestMessage CreatePhotoRequest(HttpMethod method, string key, string? versionId = null,
        byte[]? bytes = null, Guid? photoId = null)
    {
        var objectUri = BuildObjectUri(key);
        var query = versionId is null ? string.Empty : $"versionId={Uri.EscapeDataString(versionId)}";
        var uri = new Uri(objectUri.AbsoluteUri + (query.Length == 0 ? string.Empty : "?" + query));
        var request = new HttpRequestMessage(method, uri);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = bytes is null ? EmptyPayloadHash : ToHex(SHA256.HashData(bytes));
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = uri.Host,
            ["x-amz-date"] = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture),
            ["x-amz-content-sha256"] = payloadHash
        };
        if (bytes is null) headers["x-amz-checksum-mode"] = "ENABLED";
        else
        {
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            headers["content-type"] = "image/jpeg";
            headers["if-none-match"] = "*";
            headers[IntentMetadataHeader] = photoId!.Value.ToString("D");
            headers["x-amz-checksum-sha256"] = Convert.ToBase64String(SHA256.HashData(bytes));
        }
        if (!string.IsNullOrEmpty(_sessionToken)) headers["x-amz-security-token"] = _sessionToken;
        foreach (var header in headers.Where(h => h.Key != "host" && h.Key != "content-type"))
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        var signedHeaders = string.Join(';', headers.Keys);
        var canonicalHeaders = string.Concat(headers.Select(h => $"{h.Key}:{h.Value.Trim()}\n"));
        var canonical = $"{method.Method}\n{uri.AbsolutePath}\n{query}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var scope = $"{date}/{_region}/{ServiceName}/{RequestType}";
        var toSign = $"{Algorithm}\n{headers["x-amz-date"]}\n{scope}\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))}";
        var signature = ToHex(HMACSHA256.HashData(GetSigningKey(date), Encoding.UTF8.GetBytes(toSign)));
        request.Headers.TryAddWithoutValidation("Authorization", $"{Algorithm} Credential={_accessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
        return request;
    }

    private static void RequireVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || version == "null")
            throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "Versioned S3 storage is required for photos.");
    }

    private static void RequireSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var failure = response.StatusCode switch
        {
            HttpStatusCode.NotFound => S3PhotoObjectInspectionFailure.NotFound,
            HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => S3PhotoObjectInspectionFailure.Forbidden,
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests => S3PhotoObjectInspectionFailure.Transient,
            _ when response.StatusCode >= HttpStatusCode.InternalServerError => S3PhotoObjectInspectionFailure.Transient,
            _ => S3PhotoObjectInspectionFailure.InvalidResponse
        };
        throw Failure(failure, "S3 photo transport returned an unsuccessful response.");
    }
}
