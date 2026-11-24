using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using MuscleCuties.Core.Services.ExerciseMedia;

namespace MuscleCuties.UI.Infrastructure.Network;

public sealed partial class WgerApiClient : IWgerApiClient
{
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(3);
    public static readonly Uri BaseUri = new("https://wger.de/api/v2/");

    private readonly HttpClient _httpClient;
    private readonly ILogger<WgerApiClient>? _logger;

    public WgerApiClient(HttpClient httpClient, ILogger<WgerApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = BaseUri;
    }

    public async Task<IReadOnlyList<WgerExerciseInfo>> GetExercisesAsync(
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        if (!CanSendRequest())
            return [];

        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);
        var path = $"exerciseinfo/?limit={limit}&offset={offset}&language=2";

        var response = await SendAndReadAsync<WgerApiResponse<WgerExerciseInfo>>(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            WgerJsonContext.Default.WgerApiResponseWgerExerciseInfo,
            ct).ConfigureAwait(false);

        return response?.Results ?? [];
    }

    public async Task<IReadOnlyList<WgerMuscleSvgResult>> GetMuscleSvgsAsync(CancellationToken ct = default)
    {
        if (!CanSendRequest())
            return [];

        var path = "muscle/?limit=50";

        var response = await SendAndReadAsync<WgerApiResponse<WgerMuscle>>(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            WgerJsonContext.Default.WgerApiResponseWgerMuscle,
            ct).ConfigureAwait(false);

        if (response?.Results is null)
            return [];

        return response.Results.Select(m => new WgerMuscleSvgResult
        {
            Id = m.Id,
            Name = m.NameEn ?? m.Name,
            IsFront = m.IsFront,
            SvgUrl = m.ImageUrlMain ?? m.ImageUrlSecondary
        }).ToList();
    }

    public Task<Stream?> DownloadMediaStreamAsync(Uri mediaUri, CancellationToken ct = default) =>
        !CanSendRequest() || mediaUri is null
            ? Task.FromResult<Stream?>(null)
            : HttpRetryPolicy.DownloadStreamAsync(_httpClient, mediaUri,
                DefaultRateLimitDelay, MaxRateLimitDelay, ct,
                status =>
                {
                    if (_logger is not null)
                        LogWgerStatus(_logger, status);
                },
                () =>
                {
                    if (_logger is not null)
                        LogWgerTimeout(_logger);
                },
                exception =>
                {
                    if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                        LogWgerFailed(_logger, nameof(Exception), exception);
                });

    private Task<T?> SendAndReadAsync<T>(
        Func<HttpRequestMessage> createRequest,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct) =>
        HttpRetryPolicy.SendAndReadJsonAsync(_httpClient, createRequest, jsonTypeInfo,
            DefaultRateLimitDelay, MaxRateLimitDelay, ct,
            status =>
            {
                if (_logger is not null)
                    LogWgerStatus(_logger, status);
            },
            () =>
            {
                if (_logger is not null)
                    LogWgerTimeout(_logger);
            },
            exception =>
            {
                if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
                    LogWgerFailed(_logger, nameof(Exception), exception);
            });

    private bool CanSendRequest() =>
        HttpRetryPolicy.CanSendRequest(NetworkRetryHelper.HasInternetAccess,
            _logger is { } logger ? () => LogOfflineSkipped(logger) : null);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Wger API returned {StatusCode}.")]
    private static partial void LogWgerStatus(ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Wger request timed out.")]
    private static partial void LogWgerTimeout(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Wger request failed. Failure type: {ExceptionType}.")]
    private static partial void LogWgerFailed(ILogger logger, string exceptionType, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Wger request skipped because the device is offline.")]
    private static partial void LogOfflineSkipped(ILogger logger);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(WgerApiResponse<WgerExerciseInfo>))]
[JsonSerializable(typeof(WgerApiResponse<WgerMuscle>))]
[JsonSerializable(typeof(WgerApiResponse<WgerImage>))]
[JsonSerializable(typeof(WgerExerciseInfo))]
[JsonSerializable(typeof(WgerMuscle))]
[JsonSerializable(typeof(WgerImage))]
[JsonSerializable(typeof(WgerMuscleSvgResult))]
[JsonSerializable(typeof(List<WgerMuscleSvgResult>))]
internal sealed partial class WgerJsonContext : JsonSerializerContext
{
}
