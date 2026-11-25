using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using MuscleCuties.Core.Services.ExerciseMedia;

namespace MuscleCuties.UI.Infrastructure.Network;

/// <summary>Read-only client for the ExerciseDB catalog: paged exercise lists and one exercise by id, through</summary>
/// the shared retry policy.</summary>
public sealed partial class ExerciseDbClient : IExerciseDbClient
{
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(3);
    public static readonly Uri BaseUri = new("https://exercisedb.p.rapidapi.com/");

    private readonly HttpClient _httpClient;
    private readonly ILogger<ExerciseDbClient>? _logger;

    /// <summary>Takes the configured client, defaulting its base address so the relative paths resolve.</summary>
    public ExerciseDbClient(HttpClient httpClient, ILogger<ExerciseDbClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = BaseUri;
    }

    /// <summary>The exercise page the ExerciseDB API returns.</summary>
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

    /// <summary>One exercise by id, or null when the id is blank, the device is offline, or the catalog does not</summary>
    /// know it.</summary>
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

    /// <summary>Streams the media file behind the given URL.</summary>
    public Task<Stream?> DownloadMediaStreamAsync(Uri mediaUri, CancellationToken ct = default) =>
        !CanSendRequest() || mediaUri is null
            ? Task.FromResult<Stream?>(null)
            : HttpRetryPolicy.DownloadStreamAsync(_httpClient, mediaUri,
                DefaultRateLimitDelay, MaxRateLimitDelay, ct,
                status =>
                {
                    if (_logger is not null)
                        LogExerciseDbStatus(_logger, status);
                },
                () =>
                {
                    if (_logger is not null)
                        LogExerciseDbTimeout(_logger);
                },
                exception =>
                {
                    if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                        LogExerciseDbFailed(_logger, nameof(Exception), exception);
                });

    /// <summary>Sends the request and reads its JSON body.</summary>
    private Task<T?> SendAndReadAsync<T>(
        Func<HttpRequestMessage> createRequest,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct) =>
        HttpRetryPolicy.SendAndReadJsonAsync(_httpClient, createRequest, jsonTypeInfo,
            DefaultRateLimitDelay, MaxRateLimitDelay, ct,
            status =>
            {
                if (_logger is not null)
                    LogExerciseDbStatus(_logger, status);
            },
            () =>
            {
                if (_logger is not null)
                    LogExerciseDbTimeout(_logger);
            },
            exception =>
            {
                if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                    LogExerciseDbFailed(_logger, nameof(Exception), exception);
            });

    /// <summary>True when the device is online enough to send.</summary>
    private bool CanSendRequest() =>
        HttpRetryPolicy.CanSendRequest(NetworkRetryHelper.HasInternetAccess,
            _logger is { } logger ? () => LogOfflineSkipped(logger) : null);

    /// <summary>Logs the status the ExerciseDB API returned.</summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "ExerciseDB API returned {StatusCode}.")]
    private static partial void LogExerciseDbStatus(ILogger logger, HttpStatusCode statusCode);

    /// <summary>Logs that the ExerciseDB request timed out.</summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "ExerciseDB request timed out.")]
    private static partial void LogExerciseDbTimeout(ILogger logger);

    /// <summary>Logs the exception that broke the ExerciseDB request.</summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "ExerciseDB request failed. Failure type: {ExceptionType}.")]
    private static partial void LogExerciseDbFailed(ILogger logger, string exceptionType, Exception ex);

    /// <summary>Logs that the request was skipped while offline.</summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "ExerciseDB request skipped because the device is offline.")]
    private static partial void LogOfflineSkipped(ILogger logger);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(List<ExerciseDbItem>))]
[JsonSerializable(typeof(ExerciseDbItem))]
/// <summary>Source-generated JSON metadata, so the client parses without reflection.</summary>
internal sealed partial class ExerciseDbJsonContext : JsonSerializerContext
{
}
