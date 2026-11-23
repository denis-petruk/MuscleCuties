using System.Net;
using MuscleCuties.UI.Infrastructure.Network;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
///     Pins the behaviour the two catalogue clients relied on from their private SendWithRetryAsync and
///     CanSendRequest copies: two rate-limit retries, one server-error retry, and a request that is
///     refused while offline without touching the network.
/// </summary>
public sealed class HttpRetryPolicyTests
{
    [Fact]
    public async Task RateLimitedResponseIsRetriedTwiceAndTheThirdAttemptIsReturned()
    {
        var handler = new RecordingHandler(_ => HttpStatusCode.TooManyRequests);
        using var client = new HttpClient(handler);

        var response = await HttpRetryPolicy.SendWithRetryAsync(client, NewRequest, TimeSpan.Zero, TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1 + HttpRetryPolicy.MaxRateLimitRetries, handler.Attempts);
        response.Dispose();
    }

    [Fact]
    public async Task RateLimitedResponseStopsRetryingOnceTheServerRecovers()
    {
        var handler = new RecordingHandler(attempt =>
            attempt <= HttpRetryPolicy.MaxRateLimitRetries ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        var response = await HttpRetryPolicy.SendWithRetryAsync(client, NewRequest, TimeSpan.Zero, TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1 + HttpRetryPolicy.MaxRateLimitRetries, handler.Attempts);
        response.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ServerErrorIsRetriedOnceAndTheSecondAttemptIsReturned(HttpStatusCode failure)
    {
        var handler = new RecordingHandler(_ => failure);
        using var client = new HttpClient(handler);

        var response = await HttpRetryPolicy.SendWithRetryAsync(client, NewRequest, TimeSpan.Zero, TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(failure, response.StatusCode);
        Assert.Equal(1 + HttpRetryPolicy.MaxServerErrorRetries, handler.Attempts);
        response.Dispose();
    }

    [Fact]
    public async Task NotFoundIsReturnedWithoutRetrying()
    {
        var handler = new RecordingHandler(_ => HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);

        var response = await HttpRetryPolicy.SendWithRetryAsync(client, NewRequest, TimeSpan.Zero, TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
        response.Dispose();
    }

    [Fact]
    public void RateLimitDelayClampsToTheSuppliedWindow()
    {
        using var withDelta = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        withDelta.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(2),
            HttpRetryPolicy.GetRateLimitDelay(withDelta, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(3)));

        using var beyondMax = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        beyondMax.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(3),
            HttpRetryPolicy.GetRateLimitDelay(beyondMax, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(3)));

        using var withoutHeader = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        Assert.Equal(TimeSpan.FromMilliseconds(500),
            HttpRetryPolicy.GetRateLimitDelay(withoutHeader, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void OfflineRequestIsRefusedAndReportedWhileOnlineRequestIsAllowed()
    {
        var offlineLogged = 0;
        Assert.False(HttpRetryPolicy.CanSendRequest(() => false, () => offlineLogged++));
        Assert.Equal(1, offlineLogged);

        Assert.True(HttpRetryPolicy.CanSendRequest(() => true, () => offlineLogged++));
        Assert.Equal(1, offlineLogged);
    }

    private static HttpRequestMessage NewRequest() => new(HttpMethod.Get, "https://example.test/catalog");

    private sealed class RecordingHandler(Func<int, HttpStatusCode> status) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(new HttpResponseMessage(status(Attempts)));
        }
    }
}
