using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;

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

    /// <summary>Gates a request on connectivity and reports the offline case through the caller's logger, so an</summary>
    /// offline read is silent in the UI but still visible in diagnostics.</summary>
    public static bool CanSendRequest(Func<bool> hasInternetAccess, Action? logOffline = null)
    {
        ArgumentNullException.ThrowIfNull(hasInternetAccess);
        if (hasInternetAccess())
            return true;

        logOffline?.Invoke();
        return false;
    }

    /// <summary>The wait a 429 asks for, taken from its Retry-After header and clamped; the fallback is used when</summary>
    /// the server gives no usable value.</summary>
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

    /// <summary>
    /// The shared GET-and-read-JSON path the remote catalogue clients used to copy: 400/404 and any
    /// other failure yield default, timeouts and exceptions are reported through the caller's logging.
    /// </summary>
    public static async Task<T?> SendAndReadJsonAsync<T>(
        HttpClient httpClient,
        Func<HttpRequestMessage> createRequest,
        JsonTypeInfo<T> jsonTypeInfo,
        TimeSpan defaultRateLimitDelay,
        TimeSpan maxRateLimitDelay,
        CancellationToken cancellationToken,
        Action<HttpStatusCode>? onUnexpectedStatus = null,
        Action? onTimeout = null,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        try
        {
            using var response = await SendWithRetryAsync(httpClient, createRequest,
                defaultRateLimitDelay, maxRateLimitDelay, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                return default;

            if (!response.IsSuccessStatusCode)
            {
                onUnexpectedStatus?.Invoke(response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            onTimeout?.Invoke();
            return default;
        }
        catch (Exception exception)
        {
            onFailure?.Invoke(exception);
            return default;
        }
    }

    /// <summary>
    /// The shared media download both exercise clients carried byte for byte: buffered into a memory
    /// stream so the caller owns a seekable copy, with the same status/timeout/exception reporting.
    /// </summary>
    public static async Task<Stream?> DownloadStreamAsync(
        HttpClient httpClient,
        Uri uri,
        TimeSpan defaultRateLimitDelay,
        TimeSpan maxRateLimitDelay,
        CancellationToken cancellationToken,
        Action<HttpStatusCode>? onUnexpectedStatus = null,
        Action? onTimeout = null,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(uri);

        try
        {
            using var response = await SendWithRetryAsync(httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, uri),
                defaultRateLimitDelay, maxRateLimitDelay, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                onUnexpectedStatus?.Invoke(response.StatusCode);
                return null;
            }

            var memoryStream = new MemoryStream();
            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                await contentStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            onTimeout?.Invoke();
            return null;
        }
        catch (Exception exception)
        {
            onFailure?.Invoke(exception);
            return null;
        }
    }

    /// <summary>Sends the request, retrying transient and rate-limited failures.</summary>
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

    /// <summary>Keeps a server-requested delay between the fallback and the ceiling, so a hostile or mistaken</summary>
    /// header cannot stall a request for minutes.</summary>
    private static TimeSpan ClampDelay(TimeSpan delay, TimeSpan fallback, TimeSpan max)
    {
        if (delay <= TimeSpan.Zero)
            return fallback;

        return delay > max ? max : delay;
    }
}
