using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using MuscleCuties.Core.Services.ExerciseMedia;

namespace MuscleCuties.UI.Infrastructure.Network;

public sealed partial class ExerciseDbClient : IExerciseDbClient
{
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(3);
    public static readonly Uri BaseUri = new("https://exercisedb.p.rapidapi.com/");

    private readonly HttpClient _httpClient;
    private readonly ILogger<ExerciseDbClient>? _logger;

    public ExerciseDbClient(HttpClient httpClient, ILogger<ExerciseDbClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = BaseUri;
    }

    public async Task<IReadOnlyList<ExerciseDbItem>> GetExercisesAsync(
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        if (!CanSendRequest())
            return [];

        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);
        var path = $"exercises?limit={limit}&offset={offset}";

        var response = await SendAndReadAsync<List<ExerciseDbItem>>(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            ExerciseDbJsonContext.Default.ListExerciseDbItem,
            ct).ConfigureAwait(false);

        return response ?? [];
    }

    public async Task<ExerciseDbItem?> GetExerciseByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !CanSendRequest())
            return null;

        var path = $"exercises/exercise/{Uri.EscapeDataString(id.Trim())}";

        return await SendAndReadAsync<ExerciseDbItem>(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            ExerciseDbJsonContext.Default.ExerciseDbItem,
            ct).ConfigureAwait(false);
    }

    public async Task<Stream?> DownloadMediaStreamAsync(Uri mediaUri, CancellationToken ct = default)
    {
        if (!CanSendRequest() || mediaUri is null)
            return null;

        try
        {
            var response = await HttpRetryPolicy.SendWithRetryAsync(_httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, mediaUri),
                DefaultRateLimitDelay, MaxRateLimitDelay, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (_logger is not null)
                    LogExerciseDbStatus(_logger, response.StatusCode);
                response.Dispose();
                return null;
            }

            var memoryStream = new MemoryStream();
            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                await contentStream.CopyToAsync(memoryStream, ct).ConfigureAwait(false);
            }

            response.Dispose();
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (_logger is not null)
                LogExerciseDbTimeout(_logger);
            return null;
        }
        catch (Exception ex)
        {
            if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                LogExerciseDbFailed(_logger, nameof(Exception), ex);
            return null;
        }
    }

    private async Task<T?> SendAndReadAsync<T>(
        Func<HttpRequestMessage> createRequest,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct)
    {
        try
        {
            using var response = await HttpRetryPolicy.SendWithRetryAsync(_httpClient, createRequest,
                DefaultRateLimitDelay, MaxRateLimitDelay, ct).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                return default;

            if (!response.IsSuccessStatusCode)
            {
                if (_logger is not null)
                    LogExerciseDbStatus(_logger, response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync(jsonTypeInfo, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (_logger is not null)
                LogExerciseDbTimeout(_logger);
            return default;
        }
        catch (Exception ex)
        {
            if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                LogExerciseDbFailed(_logger, nameof(Exception), ex);
            return default;
        }
    }

    private bool CanSendRequest() =>
        HttpRetryPolicy.CanSendRequest(NetworkRetryHelper.HasInternetAccess,
            _logger is { } logger ? () => LogOfflineSkipped(logger) : null);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "ExerciseDB API returned {StatusCode}.")]
    private static partial void LogExerciseDbStatus(ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "ExerciseDB request timed out.")]
    private static partial void LogExerciseDbTimeout(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "ExerciseDB request failed. Failure type: {ExceptionType}.")]
    private static partial void LogExerciseDbFailed(ILogger logger, string exceptionType, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "ExerciseDB request skipped because the device is offline.")]
    private static partial void LogOfflineSkipped(ILogger logger);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(List<ExerciseDbItem>))]
[JsonSerializable(typeof(ExerciseDbItem))]
internal sealed partial class ExerciseDbJsonContext : JsonSerializerContext
{
}
