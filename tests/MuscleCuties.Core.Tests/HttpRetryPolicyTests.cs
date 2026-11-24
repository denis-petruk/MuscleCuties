using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using MuscleCuties.UI.Infrastructure.Network;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class HttpRetryPolicyTests
{
    private static readonly TimeSpan NoDelay = TimeSpan.Zero;

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ClientErrorsYieldDefaultWithoutParsing(HttpStatusCode status)
    {
        using var client = Client(_ => new HttpResponseMessage(status));

        var result = await HttpRetryPolicy.SendAndReadJsonAsync(
            client, () => new HttpRequestMessage(HttpMethod.Get, "payload"), TestJsonContext.Default.TestPayload,
            NoDelay, NoDelay, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SuccessReadsThePayload()
    {
        using var client = Client(_ => Json(HttpStatusCode.OK, """{"Name":"wger"}"""));

        var result = await HttpRetryPolicy.SendAndReadJsonAsync(
            client, () => new HttpRequestMessage(HttpMethod.Get, "payload"), TestJsonContext.Default.TestPayload,
            NoDelay, NoDelay, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("wger", result!.Name);
    }

    [Fact]
    public async Task ServerErrorsAreReportedAndYieldDefault()
    {
        HttpStatusCode reported = default;
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await HttpRetryPolicy.SendAndReadJsonAsync(
            client, () => new HttpRequestMessage(HttpMethod.Get, "payload"), TestJsonContext.Default.TestPayload,
            NoDelay, NoDelay, CancellationToken.None,
            status => reported = status);

        Assert.Null(result);
        Assert.Equal(HttpStatusCode.InternalServerError, reported);
    }

    [Fact]
    public async Task TransportFailuresAreReportedAndYieldDefault()
    {
        Exception? reported = null;
        using var client = Client(_ => throw new HttpRequestException("offline"));

        var result = await HttpRetryPolicy.SendAndReadJsonAsync(
            client, () => new HttpRequestMessage(HttpMethod.Get, "payload"), TestJsonContext.Default.TestPayload,
            NoDelay, NoDelay, CancellationToken.None,
            onFailure: exception => reported = exception);

        Assert.Null(result);
        Assert.IsType<HttpRequestException>(reported);
    }

    [Fact]
    public async Task DownloadBuffersTheContentIntoASeekableStream()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("media-bytes")
        });

        await using var stream = await HttpRetryPolicy.DownloadStreamAsync(
            client, new Uri("https://example.test/media.gif"), NoDelay, NoDelay, CancellationToken.None);

        Assert.NotNull(stream);
        Assert.Equal(0, stream!.Position);
        using var reader = new StreamReader(stream);
        Assert.Equal("media-bytes", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task DownloadFailuresAreReportedAndYieldNull()
    {
        HttpStatusCode reported = default;
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var stream = await HttpRetryPolicy.DownloadStreamAsync(
            client, new Uri("https://example.test/media.gif"), NoDelay, NoDelay, CancellationToken.None,
            status => reported = status);

        Assert.Null(stream);
        Assert.Equal(HttpStatusCode.NotFound, reported);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new StubHandler(respond)) { BaseAddress = new Uri("https://example.test/") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}

public sealed record TestPayload(string Name);

[JsonSerializable(typeof(TestPayload))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
