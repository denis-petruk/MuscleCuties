using System.Net;
using System.Net.Http;

namespace MuscleCuties.UI.Infrastructure.Network;

/// <summary>
///     Single home for the rate-limit and server-error retry policy shared by the remote catalogue
///     clients, and for the online check they gate their requests on. It stays free of MAUI types so
///     the policy can be exercised without a device runtime.
/// </summary>
internal static class HttpRetryPolicy
{
    public const int MaxRateLimitRetries = 2;
    public const int MaxServerErrorRetries = 1;

    public static bool CanSendRequest(Func<bool> hasInternetAccess, Action? logOffline = null)
    {
        ArgumentNullException.ThrowIfNull(hasInternetAccess);
        if (hasInternetAccess())
            return true;

        logOffline?.Invoke();
        return false;
    }

    public static TimeSpan GetRateLimitDelay(HttpResponseMessage response, TimeSpan fallback, TimeSpan max)
    {
        ArgumentNullException.ThrowIfNull(response);

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
            return ClampDelay(delta, fallback, max);

        if (retryAfter?.Date is { } date)
            return ClampDelay(date - DateTimeOffset.UtcNow, fallback, max);

        return fallback;
    }

    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> createRequest,
        TimeSpan defaultRateLimitDelay,
        TimeSpan maxRateLimitDelay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(createRequest);

        var rateLimitRetries = 0;
        var serverErrorRetries = 0;

        while (true)
        {
            using var request = createRequest();
            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && rateLimitRetries < MaxRateLimitRetries)
            {
                var delay = GetRateLimitDelay(response, defaultRateLimitDelay, maxRateLimitDelay);
                response.Dispose();
                rateLimitRetries++;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if ((int)response.StatusCode >= 500 && serverErrorRetries < MaxServerErrorRetries)
            {
                response.Dispose();
                serverErrorRetries++;
                await Task.Delay(defaultRateLimitDelay * serverErrorRetries, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return response;
        }
    }

    private static TimeSpan ClampDelay(TimeSpan delay, TimeSpan fallback, TimeSpan max)
    {
        if (delay <= TimeSpan.Zero)
            return fallback;

        return delay > max ? max : delay;
    }
}
