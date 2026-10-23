using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using SkiaSharp;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class ServerMediaSafetyTests
{
    private const string Bucket = "synthetic-media-test";
    private const string SourceVersion = "synthetic+source/version=";
    private const string PublishedVersion = "synthetic-published-version";
    private const int PixelDimension = 2;
    private const int ImageQuality = 90;
    private const int PublicationReplayRequestCount = 2;
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Photo = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ExactVersionReadPinsVersionAndChecksActualBytes()
    {
        var bytes = CreatePng();
        var context = Context(bytes);
        using var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("?versionId=" + Uri.EscapeDataString(SourceVersion), request.RequestUri!.Query);
            Assert.Equal("/" + context.Key, request.RequestUri.AbsolutePath);
            Assert.Contains("x-amz-checksum-mode", request.Headers.GetValues("Authorization").Single());
            return SourceResponse(bytes);
        });
        using var http = new HttpClient(handler);
        var actual = await new DefaultS3PhotoObjectInspector(http, Configuration()).ReadVersionAsync(context, SourceVersion);
        Assert.Equal(bytes, actual.ToArray());
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("checksum")]
    [InlineData("size")]
    [InlineData("version")]
    [InlineData("intent")]
    [InlineData("content-type")]
    public async Task ExactVersionReadRejectsMismatchedStorageResponse(string failure)
    {
        var bytes = CreatePng();
        var context = Context(bytes);
        using var handler = new StubHandler(_ =>
        {
            var response = SourceResponse(bytes);
            switch (failure)
            {
                case "checksum": response.Content = new ByteArrayContent(new byte[bytes.Length]); response.Content.Headers.ContentType = new("image/png"); break;
                case "size": response.Content.Headers.ContentLength = bytes.Length + 1; break;
                case "version": response.Headers.Remove("x-amz-version-id"); response.Headers.Add("x-amz-version-id", "other-version"); break;
                case "intent": response.Headers.Remove("x-amz-meta-upload-intent-id"); break;
                case "content-type": response.Content.Headers.ContentType = new("text/plain"); break;
            }
            return response;
        });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<S3PhotoObjectInspectionException>(() =>
            new DefaultS3PhotoObjectInspector(http, Configuration()).ReadVersionAsync(context, SourceVersion));
    }

    [Fact]
    public async Task ExactVersionReadBoundsActualStreamEvenWhenHeaderLies()
    {
        var bytes = CreatePng();
        using var handler = new StubHandler(_ =>
        {
            var response = SourceResponse(bytes.Concat(bytes).ToArray());
            response.Content.Headers.ContentLength = bytes.Length;
            return response;
        });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<S3PhotoObjectInspectionException>(() =>
            new DefaultS3PhotoObjectInspector(http, Configuration()).ReadVersionAsync(Context(bytes), SourceVersion));
    }

    [Fact]
    public async Task SanitizedPublicationUsesDisjointKeyAndSignedConditionalWrite()
    {
        var source = CreatePng();
        var sanitized = new SkiaPhotoByteSanitizer().Sanitize(source);
        using var handler = new StubHandler(async request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/" + PhotoObjectKeys.Published(Owner, Photo), request.RequestUri!.AbsolutePath);
            Assert.Equal("*", request.Headers.IfNoneMatch.Single().Tag);
            Assert.Contains("if-none-match", request.Headers.GetValues("Authorization").Single());
            Assert.Equal("image/jpeg", request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal(sanitized.Bytes, await request.Content.ReadAsByteArrayAsync());
            return PublishedResponse(sanitized);
        });
        using var http = new HttpClient(handler);
        var publication = await new DefaultS3PhotoObjectInspector(http, Configuration()).PublishAsync(Context(source), sanitized);
        Assert.Equal(PhotoObjectKeys.Published(Owner, Photo), publication.Key);
        Assert.NotEqual(Context(source).Key, publication.Key);
        Assert.Equal(PublishedVersion, publication.VersionId);
        Assert.Equal(sanitized.Sha256, publication.Sha256Checksum);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicationReplayAcceptsOnlyMatchingImmutableObject(bool matching)
    {
        var source = CreatePng();
        var sanitized = new SkiaPhotoByteSanitizer().Sanitize(source);
        using var handler = new StubHandler(request =>
        {
            if (request.Method == HttpMethod.Put) return new(HttpStatusCode.PreconditionFailed);
            Assert.Equal(HttpMethod.Head, request.Method);
            var response = PublishedResponse(sanitized);
            if (!matching) response.Headers.Remove("x-amz-checksum-sha256");
            return response;
        });
        using var http = new HttpClient(handler);
        var inspector = new DefaultS3PhotoObjectInspector(http, Configuration());
        if (matching) Assert.Equal(PublishedVersion, (await inspector.PublishAsync(Context(source), sanitized)).VersionId);
        else await Assert.ThrowsAsync<S3PhotoObjectInspectionException>(() => inspector.PublishAsync(Context(source), sanitized));
        Assert.Equal(PublicationReplayRequestCount, handler.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    public async Task PublicationFailsClosedWithoutVersionId(string? version)
    {
        var bytes = CreatePng();
        var sanitized = new SkiaPhotoByteSanitizer().Sanitize(bytes);
        using var handler = new StubHandler(_ =>
        {
            var response = PublishedResponse(sanitized);
            response.Headers.Remove("x-amz-version-id");
            if (version is not null) response.Headers.Add("x-amz-version-id", version);
            return response;
        });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<S3PhotoObjectInspectionException>(() =>
            new DefaultS3PhotoObjectInspector(http, Configuration()).PublishAsync(Context(bytes), sanitized));
    }

    [Fact]
    public async Task CrossAccountReadAndPublicationAreRejectedBeforeHttp()
    {
        var bytes = CreatePng();
        var crossed = Context(bytes) with { OwnerId = OtherOwner };
        using var handler = new StubHandler((HttpRequestMessage _) => Task.FromException<HttpResponseMessage>(new InvalidOperationException("No transport expected.")));
        using var http = new HttpClient(handler);
        var inspector = new DefaultS3PhotoObjectInspector(http, Configuration());
        await Assert.ThrowsAsync<ArgumentException>(() => inspector.ReadVersionAsync(crossed, SourceVersion));
        await Assert.ThrowsAsync<ArgumentException>(() => inspector.PublishAsync(crossed, new SkiaPhotoByteSanitizer().Sanitize(bytes)));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CleanupRetriesAndRemovesBothNamespacesWithoutTouchingOtherAccount()
    {
        var sourceKey = PhotoObjectKeys.Upload(Owner, Photo);
        var publishedKey = PhotoObjectKeys.Published(Owner, Photo);
        var sourceDeleted = false;
        var publishedDeleted = false;
        var firstCall = true;
        using var handler = new StubHandler(request =>
        {
            var uri = request.RequestUri!;
            Assert.DoesNotContain(OtherOwner.ToString("D"), uri.AbsoluteUri);
            if (firstCall) { firstCall = false; return new(HttpStatusCode.ServiceUnavailable); }
            if (request.Method == HttpMethod.Delete)
            {
                if (uri.AbsolutePath == "/" + sourceKey) sourceDeleted = true;
                else if (uri.AbsolutePath == "/" + publishedKey) publishedDeleted = true;
                else Assert.Fail("Unexpected cleanup key.");
                Assert.Contains("versionId=", uri.Query);
                return new(HttpStatusCode.NoContent);
            }
            var prefix = Uri.UnescapeDataString(uri.Query.TrimStart('?').Split('&').Single(p => p.StartsWith("prefix=", StringComparison.Ordinal))["prefix=".Length..]);
            var source = prefix == sourceKey;
            Assert.True(source || prefix == publishedKey);
            var key = source ? sourceKey : publishedKey;
            var deleted = source ? sourceDeleted : publishedDeleted;
            var version = deleted ? string.Empty : $"<Version><Key>{key}</Key><VersionId>synthetic-version</VersionId></Version>";
            return XmlResponse($"<ListVersionsResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><IsTruncated>false</IsTruncated>{version}</ListVersionsResult>");
        });
        using var http = new HttpClient(handler);
        var cleanup = new DefaultS3PhotoObjectCleanup(http, Configuration());
        var target = new PhotoObjectCleanupTarget(Owner, Photo, Bucket, sourceKey);
        await cleanup.DeleteAllVersionsAsync(target);
        Assert.True(sourceDeleted && publishedDeleted);
        await cleanup.DeleteAllVersionsAsync(target); // empty sweeps remain idempotent
    }

    [Fact]
    public async Task CrossAccountCleanupIsRejectedBeforeHttp()
    {
        using var handler = new StubHandler((HttpRequestMessage _) => Task.FromException<HttpResponseMessage>(new InvalidOperationException("No transport expected.")));
        using var http = new HttpClient(handler);
        var cleanup = new DefaultS3PhotoObjectCleanup(http, Configuration());
        await Assert.ThrowsAsync<S3PhotoObjectCleanupException>(() => cleanup.DeleteAllVersionsAsync(
            new(OtherOwner, Photo, Bucket, PhotoObjectKeys.Upload(Owner, Photo))));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("not an image")]
    [InlineData("")]
    public void SanitizerRejectsMalformedInput(string input) =>
        Assert.Throws<InvalidDataException>(() => new SkiaPhotoByteSanitizer().Sanitize(System.Text.Encoding.UTF8.GetBytes(input)));

    [Fact]
    public void PurgeInventoryIncludesPendingAndDeletedObjectsAndKeepsLatestGrantDeadline()
    {
        const int GrantMinutes = 15;
        const int MarginMinutes = 5;
        const int ExpectedKeys = 2;
        var now = DateTime.UtcNow;
        var grantEnd = now.AddMinutes(GrantMinutes);
        var rows = new[]
        {
            new PhotoPurgeCandidate(Photo, PhotoObjectKeys.Published(Owner, Photo), now),
            new PhotoPurgeCandidate(Photo, PhotoObjectKeys.Upload(Owner, Photo), grantEnd),
            new PhotoPurgeCandidate(Photo, PhotoObjectKeys.Upload(Owner, Photo), now)
        };
        var targets = PhotoPurgeInventory.Build(Owner, rows, now);
        Assert.Equal(ExpectedKeys, targets.Count);
        Assert.Contains(targets, t => t.Key == PhotoObjectKeys.Upload(Owner, Photo));
        Assert.Contains(targets, t => t.Key == PhotoObjectKeys.Published(Owner, Photo));
        Assert.All(targets, t => Assert.Equal(grantEnd.AddMinutes(MarginMinutes), t.NotBeforeUtc));
        Assert.Equal(targets, PhotoPurgeInventory.Build(Owner, rows, now));
    }

    [Fact]
    public void PurgeInventoryFailsClosedForMixedAccounts()
    {
        var now = DateTime.UtcNow;
        var first = new PhotoPurgeCandidate(Photo, PhotoObjectKeys.Upload(Owner, Photo), now);
        var other = new PhotoPurgeCandidate(Photo, PhotoObjectKeys.Upload(OtherOwner, Photo), now);
        Assert.Throws<InvalidOperationException>(() => PhotoPurgeInventory.Build(Owner, [first, other], now));
        Assert.All(PhotoPurgeInventory.Build(OtherOwner, [other], now),
            t => Assert.Contains(OtherOwner.ToString("D"), t.Key));
    }

    [Fact]
    public void SignedDownloadsPinPublishedVersionAndSignThatVersion()
    {
        var signer = new DefaultS3PresignedUrlService(Configuration());
        var lifetime = TimeSpan.FromMinutes(1);
        var key = PhotoObjectKeys.Published(Owner, Photo);
        var first = new Uri(signer.GeneratePresignedDownloadUrl(key, SourceVersion, lifetime));
        var second = new Uri(signer.GeneratePresignedDownloadUrl(key, PublishedVersion, lifetime));
        Assert.Equal("/" + key, first.AbsolutePath);
        Assert.Contains("versionId=" + Uri.EscapeDataString(SourceVersion), first.Query);
        Assert.NotEqual(Signature(first), Signature(second));
        Assert.Throws<ArgumentException>(() => signer.GeneratePresignedDownloadUrl(key, "null", lifetime));
    }

    [Fact]
    public async Task TwoAccountsWithSamePhotoIdentifierKeepIndependentObjectsThroughDelete()
    {
        var bytes = CreatePng();
        var first = Context(bytes);
        var second = first with { OwnerId = OtherOwner, Key = PhotoObjectKeys.Upload(OtherOwner, Photo) };
        var sanitized = new SkiaPhotoByteSanitizer().Sanitize(bytes);
        var objects = new HashSet<string> { first.Key, second.Key };
        using var handler = new StubHandler(request =>
        {
            var key = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.TrimStart('/'));
            if (request.Method == HttpMethod.Put)
            {
                Assert.True(objects.Add(key));
                return PublishedResponse(sanitized);
            }
            if (request.Method == HttpMethod.Delete)
            {
                objects.Remove(key);
                return new(HttpStatusCode.NoContent);
            }
            var prefix = Uri.UnescapeDataString(request.RequestUri.Query.TrimStart('?').Split('&')
                .Single(p => p.StartsWith("prefix=", StringComparison.Ordinal))["prefix=".Length..]);
            var version = objects.Contains(prefix)
                ? $"<Version><Key>{prefix}</Key><VersionId>synthetic-version</VersionId></Version>" : string.Empty;
            return XmlResponse($"<ListVersionsResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><IsTruncated>false</IsTruncated>{version}</ListVersionsResult>");
        });
        using var http = new HttpClient(handler);
        var inspector = new DefaultS3PhotoObjectInspector(http, Configuration());
        var firstPublished = await inspector.PublishAsync(first, sanitized);
        var otherPublished = await inspector.PublishAsync(second, sanitized);
        Assert.NotEqual(firstPublished.Key, otherPublished.Key);
        await new DefaultS3PhotoObjectCleanup(http, Configuration()).DeleteAllVersionsAsync(new(Owner, Photo, Bucket, first.Key));
        Assert.DoesNotContain(first.Key, objects);
        Assert.DoesNotContain(firstPublished.Key, objects);
        Assert.Contains(second.Key, objects);
        Assert.Contains(otherPublished.Key, objects);
    }

    private static string Signature(Uri url) => url.Query.Split('&').Single(p => p.StartsWith("X-Amz-Signature=", StringComparison.Ordinal));

    private static byte[] CreatePng()
    {
        using var bitmap = new SKBitmap(PixelDimension, PixelDimension);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, ImageQuality);
        return encoded.ToArray();
    }

    private static PhotoUploadObjectContext Context(byte[] bytes) => new(Owner, Photo, Bucket,
        PhotoObjectKeys.Upload(Owner, Photo), Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length, "image/png");

    private static HttpResponseMessage SourceResponse(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        response.Headers.Add("x-amz-meta-upload-intent-id", Photo.ToString("D"));
        response.Headers.Add("x-amz-version-id", SourceVersion);
        return response;
    }

    private static HttpResponseMessage PublishedResponse(SanitizedPhotoBytes sanitized)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(sanitized.Bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        response.Headers.Add("x-amz-checksum-sha256", Convert.ToBase64String(Convert.FromHexString(sanitized.Sha256)));
        response.Headers.Add("x-amz-meta-upload-intent-id", Photo.ToString("D"));
        response.Headers.Add("x-amz-version-id", PublishedVersion);
        return response;
    }

    private static HttpResponseMessage XmlResponse(string xml) => new(HttpStatusCode.OK) { Content = new StringContent(xml) };

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Storage:S3:BucketName"] = Bucket,
        ["Storage:S3:Region"] = "us-east-2",
        ["Storage:S3:AccessKeyId"] = "synthetic-access-key",
        ["Storage:S3:SecretAccessKey"] = "synthetic-secret-key"
    }).Build();

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : this(request => Task.FromResult(respond(request))) { }
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return respond(request);
        }
    }
}
