using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MuscleCuties.UseCases.Features.Caching;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.Infrastructure.ApiClient;

public sealed record BackendSessionTokens(
    Guid AccountId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc);

public interface IBackendSessionTokenStore
{
    Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default);
    Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default);
    Task ClearTokensAsync(CancellationToken cancellationToken = default);
}

public enum BackendApiStatus
{
    Success = 0,
    Unauthorized = 1,
    Conflict = 2,
    ValidationFailed = 3,
    NotFound = 4,
    OfflineRejected = 5,
    OfflineStaleCacheHit = 6,
    ServerError = 7
}

public sealed record BackendApiResponse<T>(
    BackendApiStatus Status,
    T? Data,
    string? ErrorCode = null,
    int? ServerConcurrencyVersion = null,
    bool IsOfflineStaleRead = false,
    DateTimeOffset? CachedAtUtc = null,
    HttpStatusCode? ResponseStatusCode = null);

public interface IMuscleCutiesBackendApiClient
{
    Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
        HttpMethod method,
        string relativePath,
        TRequest payload,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<BackendApiResponse<bool>> DeleteAsync(
        string relativePath,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}

public sealed class MuscleCutiesBackendApiClient(
    HttpClient httpClient,
    IBackendSessionTokenStore tokenStore,
    ILocalCacheService snapshotCache,
    ICloudAuthorityGate authorityGate) : IMuscleCutiesBackendApiClient
{
    private const string RefreshRoute = "/api/v1/auth/refresh";
    private const string IdempotencyHeaderName = "X-Idempotency-Key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (!authorityGate.IsNetworkOnline)
            return new BackendApiResponse<TResponse>(BackendApiStatus.OfflineRejected, default, "networkOffline");

        using var response = await SendWithRefreshRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, relativePath),
            cancellationToken).ConfigureAwait(false);

        return await MapResponseAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
        HttpMethod method,
        string relativePath,
        TRequest payload,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var writeCheck = authorityGate.EvaluateWriteAllowed($"{method.Method} {relativePath}");
        if (!writeCheck.Allowed)
        {
            return new BackendApiResponse<TResponse>(
                BackendApiStatus.OfflineRejected,
                default,
                writeCheck.RejectionCode);
        }

        using var response = await SendWithRefreshRetryAsync(() =>
        {
            var request = new HttpRequestMessage(method, relativePath)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                request.Headers.TryAddWithoutValidation(IdempotencyHeaderName, idempotencyKey);
            return request;
        }, cancellationToken).ConfigureAwait(false);

        return await MapResponseAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackendApiResponse<bool>> DeleteAsync(
        string relativePath,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var writeCheck = authorityGate.EvaluateWriteAllowed($"DELETE {relativePath}");
        if (!writeCheck.Allowed)
            return new BackendApiResponse<bool>(BackendApiStatus.OfflineRejected, false, writeCheck.RejectionCode);

        using var response = await SendWithRefreshRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, relativePath);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                request.Headers.TryAddWithoutValidation(IdempotencyHeaderName, idempotencyKey);
            return request;
        }, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
            return new BackendApiResponse<bool>(BackendApiStatus.Success, true);

        var mapped = await MapResponseAsync<object>(response, cancellationToken).ConfigureAwait(false);
        return new BackendApiResponse<bool>(mapped.Status, false, mapped.ErrorCode, mapped.ServerConcurrencyVersion);
    }

    private async Task<HttpResponseMessage> SendWithRefreshRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        using var initialRequest = requestFactory();
        if (tokens is not null && !string.IsNullOrWhiteSpace(tokens.AccessToken))
            initialRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await httpClient.SendAsync(initialRequest, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized || tokens is null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
            return response;

        response.Dispose();
        var refreshed = await TryRefreshSessionAsync(tokens, cancellationToken).ConfigureAwait(false);
        if (refreshed is null)
        {
            await tokenStore.ClearTokensAsync(cancellationToken).ConfigureAwait(false);
            await snapshotCache.ClearAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        using var retryRequest = requestFactory();
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        return await httpClient.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BackendSessionTokens?> TryRefreshSessionAsync(
        BackendSessionTokens existing,
        CancellationToken cancellationToken)
    {
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, RefreshRoute)
        {
            Content = JsonContent.Create(new { refreshToken = existing.RefreshToken }, options: JsonOptions)
        };
        using var refreshResponse = await httpClient.SendAsync(refreshRequest, cancellationToken).ConfigureAwait(false);
        if (!refreshResponse.IsSuccessStatusCode)
            return null;

        var payload = await refreshResponse.Content
            .ReadFromJsonAsync<RefreshTokenDto>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken) || string.IsNullOrWhiteSpace(payload.RefreshToken))
            return null;

        var updated = new BackendSessionTokens(
            payload.UserId != Guid.Empty ? payload.UserId : existing.AccountId,
            payload.AccessToken,
            payload.RefreshToken,
            payload.AccessTokenExpiresAtUtc);
        await tokenStore.WriteTokensAsync(updated, cancellationToken).ConfigureAwait(false);
        await snapshotCache.ActivateAccountAsync(updated.AccountId, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private static async Task<BackendApiResponse<TResponse>> MapResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent)
                return new BackendApiResponse<TResponse>(BackendApiStatus.Success, default);

            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (payload.Length == 0)
                return new BackendApiResponse<TResponse>(BackendApiStatus.Success, default);
            var data = JsonSerializer.Deserialize<TResponse>(payload, JsonOptions);
            return new BackendApiResponse<TResponse>(BackendApiStatus.Success, data);
        }

        string? errorCode = null;
        int? conflictVersion = null;
        try
        {
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
                    errorCode = errProp.GetString();
                else if (doc.RootElement.TryGetProperty("errorCode", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
                    errorCode = codeProp.GetString();
                else if (doc.RootElement.TryGetProperty("code", out var responseCodeProp) && responseCodeProp.ValueKind == JsonValueKind.String)
                    errorCode = responseCodeProp.GetString();
                else if ((doc.RootElement.TryGetProperty("suggestedPhase", out var phaseProp) ||
                          doc.RootElement.TryGetProperty("SuggestedPhase", out phaseProp)) &&
                         phaseProp.ValueKind == JsonValueKind.String)
                    errorCode = phaseProp.GetString();

                if (doc.RootElement.TryGetProperty("version", out var verProp) && verProp.TryGetInt32(out var ver))
                    conflictVersion = ver;
            }
        }
        catch (JsonException)
        {
            // Ignore non-JSON error payloads.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new BackendApiResponse<TResponse>(BackendApiStatus.Unauthorized, default, errorCode ?? "unauthorized"),
            HttpStatusCode.Conflict =>
                new BackendApiResponse<TResponse>(BackendApiStatus.Conflict, default, errorCode ?? "conflict", conflictVersion),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                new BackendApiResponse<TResponse>(BackendApiStatus.ValidationFailed, default, errorCode ?? "validationFailed"),
            HttpStatusCode.NotFound =>
                new BackendApiResponse<TResponse>(BackendApiStatus.NotFound, default, errorCode ?? "notFound"),
            HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable =>
                new BackendApiResponse<TResponse>(BackendApiStatus.ServerError, default,
                    errorCode ?? "serverError", ResponseStatusCode: response.StatusCode),
            _ => new BackendApiResponse<TResponse>(BackendApiStatus.ServerError, default, errorCode ?? "serverError")
        };
    }

    private sealed record RefreshTokenDto(
        Guid UserId,
        string AccessToken,
        string RefreshToken,
        DateTimeOffset AccessTokenExpiresAtUtc);
}

/// <summary>
/// Mobile journey gateway for PG08-02 (Auth, Profile, Onboarding, Cycle, Daily Check-In) and
/// PG08-03 (Nutrition, Workout, Dashboard, PhotoVault) with encrypted offline snapshot fallback.
/// </summary>
public sealed class MobileJourneyCloudGateway(
    IMuscleCutiesBackendApiClient apiClient,
    IBackendSessionTokenStore tokenStore,
    ILocalCacheService snapshotCache,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BackendApiResponse<CycleStateSnapshot>> GetCycleOverviewWithOfflineCacheAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        if (tokens is null || tokens.AccountId == Guid.Empty)
            return new BackendApiResponse<CycleStateSnapshot>(BackendApiStatus.Unauthorized, null, "unauthenticated");

        await snapshotCache.ActivateAccountAsync(tokens.AccountId, cancellationToken).ConfigureAwait(false);
        var from = localDate.AddDays(-35);
        var through = localDate.AddDays(35);
        var liveResponse = await apiClient.GetAsync<JsonElement>(
            $"/api/v1/cycle/overview?from={from:yyyy-MM-dd}&through={through:yyyy-MM-dd}&today={localDate:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);

        if (liveResponse.Status == BackendApiStatus.Success && liveResponse.Data.ValueKind != JsonValueKind.Undefined && liveResponse.Data.ValueKind != JsonValueKind.Null)
        {
            var snapshot = MapCycleSnapshot(liveResponse.Data, localDate);
            if (snapshot is not null)
            {
                await snapshotCache.SaveCycleStateSnapshotAsync(
                    tokens.AccountId,
                    snapshot,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                return new BackendApiResponse<CycleStateSnapshot>(BackendApiStatus.Success, snapshot);
            }
        }

        if (liveResponse.Status is BackendApiStatus.OfflineRejected or BackendApiStatus.ServerError || liveResponse.Status == BackendApiStatus.NotFound)
        {
            var cached = await snapshotCache
                .LoadCycleStateSnapshotAsync(tokens.AccountId, cancellationToken)
                .ConfigureAwait(false);
            if (cached is not null)
            {
                return new BackendApiResponse<CycleStateSnapshot>(
                    BackendApiStatus.OfflineStaleCacheHit,
                    cached.Payload,
                    ErrorCode: cached.IsStale(timeProvider.GetUtcNow()) ? "cachedSnapshotStale" : "cachedSnapshotFresh",
                    ServerConcurrencyVersion: cached.SourceVersion,
                    IsOfflineStaleRead: true,
                    CachedAtUtc: cached.CapturedAtUtc);
            }
        }

        return new BackendApiResponse<CycleStateSnapshot>(liveResponse.Status, null, liveResponse.ErrorCode, liveResponse.ServerConcurrencyVersion);
    }

    private static CycleStateSnapshot? MapCycleSnapshot(JsonElement root, DateOnly localDate)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("prediction", out var pred) || root.TryGetProperty("Prediction", out pred))
            {
                var phase = (pred.TryGetProperty("currentPhase", out var cp) || pred.TryGetProperty("CurrentPhase", out cp)) ? cp.GetString() ?? "Follicular" : "Follicular";
                var day = (pred.TryGetProperty("currentDay", out var cd) || pred.TryGetProperty("CurrentDay", out cd)) && cd.TryGetInt32(out var dInt) ? dInt : 1;
                var cycleLength = (pred.TryGetProperty("predictedCycleLength", out var cl) || pred.TryGetProperty("PredictedCycleLength", out cl)) && cl.TryGetInt32(out var clInt) ? clInt : 28;
                var source = (pred.TryGetProperty("predictionSource", out var ps) || pred.TryGetProperty("PredictionSource", out ps)) ? ps.GetString() ?? "default" : "default";
                DateOnly? nextPeriod = (pred.TryGetProperty("predictedNextPeriodDate", out var np) || pred.TryGetProperty("PredictedNextPeriodDate", out np)) && np.ValueKind == JsonValueKind.String && DateOnly.TryParse(np.GetString(), out var npDate) ? npDate : null;
                return new CycleStateSnapshot(localDate, phase, day, cycleLength, source, nextPeriod, ConcurrencyVersion: 1);
            }

            try
            {
                return JsonSerializer.Deserialize<CycleStateSnapshot>(root.GetRawText(), JsonOptions);
            }
            catch
            {
                // Fallback
            }
        }

        return null;
    }

    public async Task<BackendApiResponse<DailyNutritionTargetsSnapshot>> GetNutritionTargetsWithOfflineCacheAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        if (tokens is null || tokens.AccountId == Guid.Empty)
            return new BackendApiResponse<DailyNutritionTargetsSnapshot>(BackendApiStatus.Unauthorized, null, "unauthenticated");

        await snapshotCache.ActivateAccountAsync(tokens.AccountId, cancellationToken).ConfigureAwait(false);
        var liveResponse = await apiClient.GetAsync<JsonElement>(
            $"/api/v1/nutrition/targets?date={localDate:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);

        if (liveResponse.Status == BackendApiStatus.Success && liveResponse.Data.ValueKind != JsonValueKind.Undefined && liveResponse.Data.ValueKind != JsonValueKind.Null)
        {
            var snapshot = MapNutritionTargetsSnapshot(liveResponse.Data, localDate);
            if (snapshot is not null)
            {
                await snapshotCache.SaveNutritionTargetsSnapshotAsync(
                    tokens.AccountId,
                    snapshot,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                return new BackendApiResponse<DailyNutritionTargetsSnapshot>(BackendApiStatus.Success, snapshot);
            }
        }

        if (liveResponse.Status is BackendApiStatus.OfflineRejected or BackendApiStatus.ServerError || liveResponse.Status == BackendApiStatus.NotFound)
        {
            var cached = await snapshotCache
                .LoadNutritionTargetsSnapshotAsync(tokens.AccountId, cancellationToken)
                .ConfigureAwait(false);
            if (cached is not null)
            {
                return new BackendApiResponse<DailyNutritionTargetsSnapshot>(
                    BackendApiStatus.OfflineStaleCacheHit,
                    cached.Payload,
                    ErrorCode: cached.IsStale(timeProvider.GetUtcNow()) ? "cachedSnapshotStale" : "cachedSnapshotFresh",
                    ServerConcurrencyVersion: cached.SourceVersion,
                    IsOfflineStaleRead: true,
                    CachedAtUtc: cached.CapturedAtUtc);
            }
        }

        return new BackendApiResponse<DailyNutritionTargetsSnapshot>(liveResponse.Status, null, liveResponse.ErrorCode, liveResponse.ServerConcurrencyVersion);
    }

    private static DailyNutritionTargetsSnapshot? MapNutritionTargetsSnapshot(JsonElement root, DateOnly localDate)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("calories", out var calProp) || root.TryGetProperty("Calories", out calProp))
            {
                var cals = calProp.TryGetInt32(out var ci) ? ci : (int)calProp.GetSingle();
                var protein = (root.TryGetProperty("protein", out var pProp) || root.TryGetProperty("Protein", out pProp)) ? (decimal)pProp.GetSingle() : 0m;
                var carbs = (root.TryGetProperty("carbs", out var cbProp) || root.TryGetProperty("Carbs", out cbProp)) ? (decimal)cbProp.GetSingle() : 0m;
                var fats = (root.TryGetProperty("fats", out var fProp) || root.TryGetProperty("Fats", out fProp)) ? (decimal)fProp.GetSingle() : 0m;
                var src = (root.TryGetProperty("source", out var sProp) || root.TryGetProperty("Source", out sProp)) ? sProp.GetString() ?? "calculated" : "calculated";
                return new DailyNutritionTargetsSnapshot(localDate, cals, protein, carbs, fats, src, ConcurrencyVersion: 1);
            }

            try
            {
                return JsonSerializer.Deserialize<DailyNutritionTargetsSnapshot>(root.GetRawText(), JsonOptions);
            }
            catch
            {
                // Fallback
            }
        }

        return null;
    }

    public async Task<BackendApiResponse<ActiveWorkoutSessionSnapshot>> GetActiveWorkoutWithOfflineCacheAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        if (tokens is null || tokens.AccountId == Guid.Empty)
            return new BackendApiResponse<ActiveWorkoutSessionSnapshot>(BackendApiStatus.Unauthorized, null, "unauthenticated");

        await snapshotCache.ActivateAccountAsync(tokens.AccountId, cancellationToken).ConfigureAwait(false);
        var liveResponse = await apiClient.GetAsync<JsonElement>(
            $"/api/v1/workouts/sessions/{sessionId:D}",
            cancellationToken).ConfigureAwait(false);

        if (liveResponse.Status == BackendApiStatus.Success && liveResponse.Data.ValueKind != JsonValueKind.Undefined && liveResponse.Data.ValueKind != JsonValueKind.Null)
        {
            var snapshot = MapActiveWorkoutSnapshot(liveResponse.Data, sessionId);
            if (snapshot is not null)
            {
                await snapshotCache.SaveActiveWorkoutSnapshotAsync(
                    tokens.AccountId,
                    snapshot,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                return new BackendApiResponse<ActiveWorkoutSessionSnapshot>(BackendApiStatus.Success, snapshot);
            }
        }

        if (liveResponse.Status is BackendApiStatus.OfflineRejected or BackendApiStatus.ServerError || liveResponse.Status == BackendApiStatus.NotFound)
        {
            var cached = await snapshotCache
                .LoadActiveWorkoutSnapshotAsync(tokens.AccountId, cancellationToken)
                .ConfigureAwait(false);
            if (cached is not null && cached.Payload.SessionId == sessionId)
            {
                return new BackendApiResponse<ActiveWorkoutSessionSnapshot>(
                    BackendApiStatus.OfflineStaleCacheHit,
                    cached.Payload,
                    ErrorCode: cached.IsStale(timeProvider.GetUtcNow()) ? "cachedSnapshotStale" : "cachedSnapshotFresh",
                    ServerConcurrencyVersion: cached.SourceVersion,
                    IsOfflineStaleRead: true,
                    CachedAtUtc: cached.CapturedAtUtc);
            }
        }

        return new BackendApiResponse<ActiveWorkoutSessionSnapshot>(liveResponse.Status, null, liveResponse.ErrorCode, liveResponse.ServerConcurrencyVersion);
    }

    private static ActiveWorkoutSessionSnapshot? MapActiveWorkoutSnapshot(JsonElement root, Guid sessionId)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("workoutName", out var nameProp) || root.TryGetProperty("WorkoutName", out nameProp))
            {
                var name = nameProp.GetString() ?? "Workout";
                var duration = (root.TryGetProperty("totalDurationSeconds", out var durProp) || root.TryGetProperty("TotalDurationSeconds", out durProp)) && durProp.TryGetInt32(out var d) ? d : 0;
                var exercises = new List<ActiveWorkoutExerciseSnapshot>();
                if ((root.TryGetProperty("sets", out var setsProp) || root.TryGetProperty("Sets", out setsProp)) && setsProp.ValueKind == JsonValueKind.Array)
                {
                    var grouped = new Dictionary<string, List<ActiveWorkoutSetSnapshot>>();
                    foreach (var s in setsProp.EnumerateArray())
                    {
                        var exName = (s.TryGetProperty("exerciseName", out var en) || s.TryGetProperty("ExerciseName", out en)) ? en.GetString() ?? "Exercise" : "Exercise";
                        var setNum = (s.TryGetProperty("setNumber", out var sn) || s.TryGetProperty("SetNumber", out sn)) && sn.TryGetInt32(out var snVal) ? snVal : 1;
                        var reps = (s.TryGetProperty("repsCompleted", out var rc) || s.TryGetProperty("RepsCompleted", out rc)) && rc.TryGetInt32(out var rcVal) ? rcVal : 0;
                        var wt = (s.TryGetProperty("weightKg", out var wk) || s.TryGetProperty("WeightKg", out wk)) && wk.TryGetDecimal(out var wkVal) ? wkVal : 0m;
                        decimal? rpe = (s.TryGetProperty("rpe", out var rp) || s.TryGetProperty("Rpe", out rp)) && rp.ValueKind == JsonValueKind.Number && rp.TryGetDecimal(out var rpeVal) ? rpeVal : null;
                        if (!grouped.TryGetValue(exName, out var list))
                        {
                            list = new List<ActiveWorkoutSetSnapshot>();
                            grouped[exName] = list;
                        }
                        list.Add(new ActiveWorkoutSetSnapshot(setNum, reps, wt, rpe, true));
                    }
                    foreach (var (k, v) in grouped)
                    {
                        exercises.Add(new ActiveWorkoutExerciseSnapshot(k.ToUpperInvariant().Replace(" ", "_"), k, v));
                    }
                }

                return new ActiveWorkoutSessionSnapshot(
                    sessionId,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    name,
                    duration,
                    exercises,
                    ConcurrencyVersion: 1);
            }

            try
            {
                return JsonSerializer.Deserialize<ActiveWorkoutSessionSnapshot>(root.GetRawText(), JsonOptions);
            }
            catch
            {
                // Fallback
            }
        }

        return null;
    }

    public async Task<BackendApiResponse<BackendSessionTokens>> SignInWithPasswordAsync(
        string emailOrUsername,
        string password,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.SendMutationAsync<object, JsonElement>(
            HttpMethod.Post,
            "/api/v1/auth/password",
            new { emailOrUsername, password },
            idempotencyKey: null,
            cancellationToken).ConfigureAwait(false);

        return await ReadSessionResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackendApiResponse<BackendSessionTokens>> SignInWithAppleAsync(
        string identityToken,
        string nonce,
        string nonceChallengeId,
        CancellationToken cancellationToken = default,
        string? authorizationCode = null)
    {
        if (string.IsNullOrWhiteSpace(identityToken) || string.IsNullOrWhiteSpace(nonce) ||
            !Guid.TryParse(nonceChallengeId, out _))
            return new BackendApiResponse<BackendSessionTokens>(BackendApiStatus.ValidationFailed, null,
                "missingAppleCredential");

        var response = await apiClient.SendMutationAsync<object, JsonElement>(
            HttpMethod.Post,
            "/api/v1/auth/apple",
            new { identityToken, nonce, nonceChallengeId, authorizationCode },
            idempotencyKey: null,
            cancellationToken).ConfigureAwait(false);

        return await ReadSessionResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BackendApiResponse<BackendSessionTokens>> ReadSessionResponseAsync(
        BackendApiResponse<JsonElement> response,
        CancellationToken cancellationToken)
    {

        if (response.Status == BackendApiStatus.Success && response.Data.ValueKind == JsonValueKind.Object)
        {
            var userId = Guid.Empty;
            var hasUserId = response.Data.TryGetProperty("userId", out var uProp) &&
                            uProp.ValueKind == JsonValueKind.String && uProp.TryGetGuid(out userId);
            var accessToken = response.Data.TryGetProperty("accessToken", out var atProp) &&
                              atProp.ValueKind == JsonValueKind.String ? atProp.GetString() ?? "" : "";
            var refreshToken = response.Data.TryGetProperty("refreshToken", out var rtProp) &&
                               rtProp.ValueKind == JsonValueKind.String ? rtProp.GetString() ?? "" : "";
            var expires = default(DateTimeOffset);
            var hasExpiration = response.Data.TryGetProperty("accessExpiresAtUtc", out var expProp) &&
                                expProp.ValueKind == JsonValueKind.String &&
                                expProp.TryGetDateTimeOffset(out expires);
            if (!hasUserId || userId == Guid.Empty || string.IsNullOrWhiteSpace(accessToken) ||
                string.IsNullOrWhiteSpace(refreshToken) || !hasExpiration || expires <= DateTimeOffset.UtcNow)
                return new BackendApiResponse<BackendSessionTokens>(BackendApiStatus.ServerError, null,
                    "invalidSessionResponse");

            var tokens = new BackendSessionTokens(userId, accessToken, refreshToken, expires);
            await tokenStore.WriteTokensAsync(tokens, cancellationToken).ConfigureAwait(false);
            await snapshotCache.ActivateAccountAsync(tokens.AccountId, cancellationToken).ConfigureAwait(false);
            return new BackendApiResponse<BackendSessionTokens>(BackendApiStatus.Success, tokens);
        }

        if (response.Status == BackendApiStatus.Success)
            return new BackendApiResponse<BackendSessionTokens>(BackendApiStatus.ServerError, null,
                "invalidSessionResponse");

        return new BackendApiResponse<BackendSessionTokens>(response.Status, null, response.ErrorCode);
    }

    public async Task<BackendApiResponse<DashboardOverviewResponse>> GetDashboardOverviewAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        return await GetDashboardOverviewAsync(today, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackendApiResponse<DashboardOverviewResponse>> GetDashboardOverviewAsync(
        DateOnly today,
        string? timeZoneId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        if (tokens is null || tokens.AccountId == Guid.Empty)
            return new BackendApiResponse<DashboardOverviewResponse>(BackendApiStatus.Unauthorized, null, "unauthenticated");

        var timeZoneQuery = string.IsNullOrWhiteSpace(timeZoneId)
            ? string.Empty
            : $"&timeZoneId={Uri.EscapeDataString(timeZoneId)}";
        return await apiClient.GetAsync<DashboardOverviewResponse>(
            $"/api/v1/dashboard/overview?date={today:yyyy-MM-dd}{timeZoneQuery}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackendApiResponse<DashboardCalendarResponse>> GetDashboardCalendarAsync(
        int year,
        int month,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        if (tokens is null || tokens.AccountId == Guid.Empty)
            return new BackendApiResponse<DashboardCalendarResponse>(BackendApiStatus.Unauthorized, null, "unauthenticated");

        return await apiClient.GetAsync<DashboardCalendarResponse>(
            $"/api/v1/dashboard/calendar?year={year}&month={month}&today={today:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);
    }

    public Task<BackendApiResponse<TResponse>> SaveJourneyMutationAsync<TRequest, TResponse>(
        HttpMethod method,
        string relativePath,
        TRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        apiClient.SendMutationAsync<TRequest, TResponse>(method, relativePath, request, idempotencyKey, cancellationToken);

    public async Task LogoutAndPurgeLocalStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
            if (tokens is not null && !string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                await apiClient.SendMutationAsync<object, object>(
                    HttpMethod.Post,
                    "/api/v1/auth/revoke",
                    new { refreshToken = tokens.RefreshToken },
                    idempotencyKey: null,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await tokenStore.ClearTokensAsync(cancellationToken).ConfigureAwait(false);
            await snapshotCache.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
