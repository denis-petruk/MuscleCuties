using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed class DefaultS3PresignedUrlService : IS3PresignedUrlService
{
    private const string DefaultBucket = "musclecuties-media";
    private const string DefaultRegion = "us-east-1";
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string ServiceName = "s3";
    private const string RequestType = "aws4_request";
    private const string UploadIntentMetadataField = "x-amz-meta-upload-intent-id";
    private const string ChecksumAlgorithmField = "x-amz-checksum-algorithm";
    private const string ChecksumSha256Field = "x-amz-checksum-sha256";
    private const string Sha256AlgorithmName = "SHA256";
    private const int Sha256HexCharacterCount = 64;
    private const int MinimumUploadBytes = 1;
    private const int MaximumUploadBytes = 15 * 1024 * 1024;
    private const int MinimumPostExpirationSeconds = 60;
    private const int MaximumPostExpirationSeconds = 900;
    private static readonly HashSet<string> ApprovedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/heic", "image/webp"
    };

    private readonly string _bucket;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string? _sessionToken;

    public DefaultS3PresignedUrlService(IConfiguration configuration)
    {
        _bucket = configuration["Storage:S3:BucketName"] ?? DefaultBucket;
        _region = configuration["Storage:S3:Region"] ?? DefaultRegion;
        _accessKey = RequireCredential(configuration, "Storage:S3:AccessKeyId", "AWS_ACCESS_KEY_ID");
        _secretKey = RequireCredential(configuration, "Storage:S3:SecretAccessKey", "AWS_SECRET_ACCESS_KEY");
        _sessionToken = configuration["Storage:S3:SessionToken"] ?? configuration["AWS_SESSION_TOKEN"];
    }

    public string GeneratePresignedUploadUrl(string s3Key, string contentType, TimeSpan expiration)
    {
        PhotoObjectKeys.RequireUpload(s3Key);
        return BuildPresignedUrl(s3Key, "PUT", expiration, contentType);
    }

    public S3PresignedPost GeneratePresignedUploadPost(string s3Key, string contentType, string uploadIntentId, string sha256Checksum, TimeSpan expiration)
    {
        var photoId = PhotoObjectKeys.RequireUpload(s3Key);
        if (!Guid.TryParseExact(uploadIntentId, "D", out var intentId) || intentId != photoId)
            throw new ArgumentException("The upload grant must match its owner-bound object key.", nameof(uploadIntentId));
        if (string.IsNullOrWhiteSpace(s3Key) || s3Key.StartsWith('/'))
            throw new ArgumentException("An exact relative S3 object key is required.", nameof(s3Key));
        if (!ApprovedImageContentTypes.Contains(contentType))
            throw new ArgumentException("The content type is not an approved photo format.", nameof(contentType));
        if (string.IsNullOrWhiteSpace(uploadIntentId))
            throw new ArgumentException("An upload intent identifier is required.", nameof(uploadIntentId));
        if (!IsSha256Hex(sha256Checksum))
            throw new ArgumentException("A SHA-256 checksum must be 64 hexadecimal characters.", nameof(sha256Checksum));

        var checksumBase64 = Convert.ToBase64String(Convert.FromHexString(sha256Checksum));

        var now = DateTime.UtcNow;
        var expirationSeconds = (int)Math.Clamp(expiration.TotalSeconds, MinimumPostExpirationSeconds, MaximumPostExpirationSeconds);
        var expiryWithFraction = now.AddSeconds(expirationSeconds);
        var expiresAtUtc = new DateTime(expiryWithFraction.Ticks - expiryWithFraction.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var credentialScope = $"{dateStamp}/{_region}/{ServiceName}/{RequestType}";
        var credential = $"{_accessKey}/{credentialScope}";

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Content-Type"] = contentType,
            ["key"] = s3Key,
            ["x-amz-algorithm"] = Algorithm,
            ["x-amz-credential"] = credential,
            ["x-amz-date"] = amzDate,
            [UploadIntentMetadataField] = uploadIntentId,
            [ChecksumAlgorithmField] = Sha256AlgorithmName,
            [ChecksumSha256Field] = checksumBase64
        };
        if (!string.IsNullOrEmpty(_sessionToken))
            fields["x-amz-security-token"] = _sessionToken;

        var policy = BuildPostPolicy(expiresAtUtc, fields);
        var encodedPolicy = Convert.ToBase64String(Encoding.UTF8.GetBytes(policy));
        var signingKey = GetSignatureKey(_secretKey, dateStamp, _region, ServiceName);
        var signature = ToHex(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(encodedPolicy)));
        fields["policy"] = encodedPolicy;
        fields["x-amz-signature"] = signature;

        var host = $"{_bucket}.s3.{_region}.amazonaws.com";
        return new S3PresignedPost($"https://{host}/", fields, expiresAtUtc);
    }

    public string GeneratePresignedDownloadUrl(string s3Key, TimeSpan expiration)
    {
        return BuildPresignedUrl(s3Key, "GET", expiration, null);
    }

    public string GeneratePresignedDownloadUrl(string s3Key, string versionId, TimeSpan expiration)
    {
        if (string.IsNullOrWhiteSpace(versionId) || versionId == "null")
            throw new ArgumentException("A published S3 object version is required.", nameof(versionId));
        return BuildPresignedUrl(s3Key, "GET", expiration, null, versionId);
    }

    public string GeneratePresignedDeleteUrl(string s3Key, TimeSpan expiration)
    {
        return BuildPresignedUrl(s3Key, "DELETE", expiration, null);
    }

    private string BuildPresignedUrl(string s3Key, string httpMethod, TimeSpan expiration, string? contentType, string? versionId = null)
    {
        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var credentialScope = $"{dateStamp}/{_region}/{ServiceName}/{RequestType}";
        var cleanKey = s3Key.TrimStart('/');
        var host = $"{_bucket}.s3.{_region}.amazonaws.com";
        var expirationSeconds = (int)Math.Clamp(expiration.TotalSeconds, 60, 604800); // 1 min to 7 days

        var queryParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = Algorithm,
            ["X-Amz-Credential"] = $"{_accessKey}/{credentialScope}",
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = expirationSeconds.ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host"
        };

        if (!string.IsNullOrEmpty(_sessionToken))
            queryParams["X-Amz-Security-Token"] = _sessionToken;
        if (versionId is not null)
            queryParams["versionId"] = versionId;

        var signedHeaders = contentType is null ? "host" : "content-type;host";
        queryParams["X-Amz-SignedHeaders"] = signedHeaders;

        var canonicalQueryString = string.Join("&", queryParams.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        var canonicalHeaders = contentType is null
            ? $"host:{host}\n"
            : $"content-type:{NormalizeHeaderValue(contentType)}\nhost:{host}\n";

        var canonicalRequest = $"{httpMethod}\n/{cleanKey}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\nUNSIGNED-PAYLOAD";
        var hashedCanonicalRequest = ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));

        var stringToSign = $"{Algorithm}\n{amzDate}\n{credentialScope}\n{hashedCanonicalRequest}";

        var signingKey = GetSignatureKey(_secretKey, dateStamp, _region, ServiceName);
        var signature = ToHex(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));

        return $"https://{host}/{cleanKey}?{canonicalQueryString}&X-Amz-Signature={signature}";
    }

    private static string RequireCredential(IConfiguration configuration, string key, string awsEnvironmentKey)
    {
        var value = configuration[key] ?? configuration[awsEnvironmentKey];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Required S3 credential configuration '{key}' or '{awsEnvironmentKey}' is missing.");
        return value;
    }

    private string BuildPostPolicy(DateTime expiresAtUtc, IReadOnlyDictionary<string, string> fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("expiration", expiresAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            writer.WriteStartArray("conditions");
            writer.WriteStartObject();
            writer.WriteString("bucket", _bucket);
            writer.WriteEndObject();
            foreach (var field in fields)
            {
                writer.WriteStartObject();
                writer.WriteString(field.Key, field.Value);
                writer.WriteEndObject();
            }
            writer.WriteStartArray();
            writer.WriteStringValue("content-length-range");
            writer.WriteNumberValue(MinimumUploadBytes);
            writer.WriteNumberValue(MaximumUploadBytes);
            writer.WriteEndArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string NormalizeHeaderValue(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool IsSha256Hex(string? value)
    {
        if (value is null || value.Length != Sha256HexCharacterCount)
            return false;
        try
        {
            return Convert.FromHexString(value).Length == Sha256HexCharacterCount / 2;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(regionName));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(serviceName));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes(RequestType));
    }

    private static string ToHex(byte[] data)
    {
        var builder = new StringBuilder(data.Length * 2);
        foreach (var b in data)
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
