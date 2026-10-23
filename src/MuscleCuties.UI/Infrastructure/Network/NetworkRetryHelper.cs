using System.Net.Http;
using Microsoft.Maui.Networking;

namespace MuscleCuties.UI.Infrastructure.Network;

internal static class NetworkRetryHelper
{
    public static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(30);

    public static bool HasInternetAccess()
    {
        try
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
        catch (Exception ex) when (ex is FeatureNotEnabledException or FeatureNotSupportedException)
        {
            return true;
        }
    }

    public static TimeSpan GetRateLimitDelay(HttpResponseMessage response, TimeSpan? defaultDelay = null,
        TimeSpan? maxDelay = null)
    {
        var fallback = defaultDelay ?? DefaultRateLimitDelay;
        var max = maxDelay ?? MaxRateLimitDelay;

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
            return ClampDelay(delta, fallback, max);

        if (retryAfter?.Date is { } date)
            return ClampDelay(date - DateTimeOffset.UtcNow, fallback, max);

        return fallback;
    }

    private static TimeSpan ClampDelay(TimeSpan delay, TimeSpan fallback, TimeSpan max)
    {
        if (delay <= TimeSpan.Zero)
            return fallback;

        return delay > max ? max : delay;
    }
}
