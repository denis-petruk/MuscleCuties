using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>Inspects uploads and transports exact-version bytes for server-only publication.</summary>
public sealed partial class DefaultS3PhotoObjectInspector : IS3PhotoObjectInspector
{
    private const string DefaultBucket = "musclecuties-media";
    private const string DefaultRegion = "us-east-1";
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string ServiceName = "s3";
    private const string RequestType = "aws4_request";
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string IntentMetadataHeader = "x-amz-meta-upload-intent-id";

    private readonly HttpClient _httpClient;
    private readonly string _bucket;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string? _sessionToken;
    private readonly TimeProvider _timeProvider;

    public DefaultS3PhotoObjectInspector(HttpClient httpClient, IConfiguration configuration)
        : this(httpClient, configuration, TimeProvider.System)
    {
    }

    internal DefaultS3PhotoObjectInspector(HttpClient httpClient, IConfiguration configuration, TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _bucket = configuration["Storage:S3:BucketName"] ?? DefaultBucket;
        _region = configuration["Storage:S3:Region"] ?? DefaultRegion;
        _accessKey = RequiredCredential(configuration, "Storage:S3:AccessKeyId", "AWS_ACCESS_KEY_ID");
        _secretKey = RequiredCredential(configuration, "Storage:S3:SecretAccessKey", "AWS_SECRET_ACCESS_KEY");
        _sessionToken = configuration["Storage:S3:SessionToken"] ?? configuration["AWS_SESSION_TOKEN"];
    }

    public async Task<S3PhotoObjectInspection> InspectAsync(
        PhotoUploadObjectContext context,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        using var request = CreatePhotoRequest(HttpMethod.Head, context.Key);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PhotoTransportTimeout);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.Transient, "S3 object inspection transport failed.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.Transient, "S3 object inspection timed out.", exception);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw Failure(S3PhotoObjectInspectionFailure.NotFound, "The uploaded S3 object was not found.");
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
                throw Failure(S3PhotoObjectInspectionFailure.Forbidden, "S3 denied access to the uploaded object.");
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout || (int)response.StatusCode == 429)
                throw Failure(S3PhotoObjectInspectionFailure.Transient, "S3 object inspection returned a transient response.");
            if (!response.IsSuccessStatusCode)
                throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "S3 object inspection returned an unexpected response.");

            var headers = response.Headers;
            var checksum = GetHeader(headers, "x-amz-checksum-sha256");
            var uploadIntentId = GetHeader(headers, IntentMetadataHeader);
            var contentLength = response.Content.Headers.ContentLength;
            var contentType = response.Content.Headers.ContentType?.ToString();
            if (contentLength is null || contentLength < 0 || string.IsNullOrWhiteSpace(checksum))
                throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "S3 omitted required measured object metadata.");
            if (!string.Equals(uploadIntentId, context.PhotoId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                throw Failure(S3PhotoObjectInspectionFailure.Forbidden, "S3 object intent metadata did not match the owner-bound intent.");
            try
            {
                var measuredChecksum = Convert.FromBase64String(checksum);
                if (measuredChecksum.Length != 32)
                    throw new FormatException();
                var expectedChecksum = Convert.FromHexString(context.ExpectedSha256ChecksumHex);
                if (!CryptographicOperations.FixedTimeEquals(measuredChecksum, expectedChecksum))
                    throw Failure(S3PhotoObjectInspectionFailure.InvalidResponse, "S3 object checksum did not match the owner-bound upload intent.");
            }
            catch (FormatException exception)
            {
                throw new S3PhotoObjectInspectionException(S3PhotoObjectInspectionFailure.InvalidResponse, "S3 returned an invalid SHA-256 checksum.", exception);
            }

            var version = GetHeader(headers, "x-amz-version-id");
            RequireVersion(version);
            return new S3PhotoObjectInspection(
                contentLength.Value,
                contentType,
                uploadIntentId,
                checksum,
                version);
        }
    }

    private void ValidateContext(PhotoUploadObjectContext context)
    {
        if (context is null || context.OwnerId == Guid.Empty || context.PhotoId == Guid.Empty)
            throw new ArgumentException("An owner-bound upload intent context is required.", nameof(context));
        if (!string.Equals(context.Bucket, _bucket, StringComparison.Ordinal))
            throw new ArgumentException("The upload intent bucket does not match the configured bucket.", nameof(context));
        PhotoObjectKeys.RequireUpload(context.OwnerId, context.PhotoId, context.Key);
        if (context.ExpectedSha256ChecksumHex.Length != 64 || !context.ExpectedSha256ChecksumHex.All(Uri.IsHexDigit))
            throw new ArgumentException("The upload intent must include a SHA-256 checksum.", nameof(context));
        if (context.DeclaredSizeBytes is <= 0 or > MaximumTransportBytes)
            throw new ArgumentException("The upload intent size is outside supported bounds.", nameof(context));
    }

    private Uri BuildObjectUri(string key)
    {
        var host = $"{_bucket}.s3.{_region}.amazonaws.com";
        var path = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"https://{host}/{path}", UriKind.Absolute);
    }

    private byte[] GetSigningKey(string dateStamp)
    {
        var dateKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("AWS4" + _secretKey), Encoding.UTF8.GetBytes(dateStamp));
        var regionKey = HMACSHA256.HashData(dateKey, Encoding.UTF8.GetBytes(_region));
        var serviceKey = HMACSHA256.HashData(regionKey, Encoding.UTF8.GetBytes(ServiceName));
        return HMACSHA256.HashData(serviceKey, Encoding.UTF8.GetBytes(RequestType));
    }

    private static string RequiredCredential(IConfiguration configuration, string key, string environmentKey)
    {
        var value = configuration[key] ?? configuration[environmentKey];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Required S3 credential configuration '{key}' or '{environmentKey}' is missing.")
            : value;
    }

    private static string? GetHeader(System.Net.Http.Headers.HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static S3PhotoObjectInspectionException Failure(S3PhotoObjectInspectionFailure failure, string message) => new(failure, message);

    private static string ToHex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();
}
