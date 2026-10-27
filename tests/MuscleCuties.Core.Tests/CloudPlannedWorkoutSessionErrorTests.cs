using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;

namespace MuscleCuties.Core.Tests;

public sealed class CloudPlannedWorkoutSessionErrorTests
{
    [Fact]
    public async Task OfflineStartIsAConnectionFailure()
    {
        var store = new CloudWorkoutSessionActionStore(new SessionApi(BackendApiStatus.OfflineRejected));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            store.StartPlannedSessionAsync(1, Guid.NewGuid(), DateTime.UnixEpoch, CancellationToken.None));
    }

    [Fact]
    public async Task ServerConflictIsNotMisreportedAsConnectionFailure()
    {
        var store = new CloudWorkoutSessionActionStore(new SessionApi(BackendApiStatus.Conflict));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.StartPlannedSessionAsync(1, Guid.NewGuid(), DateTime.UnixEpoch, CancellationToken.None));
    }

    [Fact]
    public async Task UnsafeActivityCannotResumeAndIsIdentifiedSeparately()
    {
        var store = new CloudWorkoutSessionActionStore(
            new SessionApi(BackendApiStatus.Conflict, "WorkoutActivityNoLongerSafe"));
        await Assert.ThrowsAsync<WorkoutActivityNoLongerSafeException>(() =>
            store.ResumePlannedTimingAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredTimingIntervalRequiresExplicitResume()
    {
        var store = new CloudWorkoutSessionActionStore(
            new SessionApi(BackendApiStatus.Conflict, "WorkoutTimingIntervalExpired"));
        await Assert.ThrowsAsync<WorkoutTimingIntervalExpiredException>(() =>
            store.CheckpointPlannedTimingAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 15, true,
                CancellationToken.None));
    }

    [Fact]
    public async Task CheckpointRetryUsesSameOperationAndMeasuredPayload()
    {
        var sessionId = Guid.NewGuid();
        var intervalId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var api = new TimingApi(sessionId);
        var store = new CloudWorkoutSessionActionStore(api);

        await store.CheckpointPlannedTimingAsync(sessionId, operationId, intervalId, 15, true, CancellationToken.None);
        await store.CheckpointPlannedTimingAsync(sessionId, operationId, intervalId, 15, true, CancellationToken.None);

        Assert.Equal(2, api.Requests.Count);
        Assert.All(api.Requests, item =>
        {
            Assert.Equal(operationId.ToString("N"), item.Key);
            Assert.Equal(intervalId, item.Body.IntervalId);
            Assert.Equal(15, item.Body.MeasuredSeconds);
            Assert.True(item.Body.Pause);
        });
    }

    private sealed class TimingApi(Guid sessionId) : IMuscleCutiesBackendApiClient
    {
        public List<(string? Key, CheckpointWorkoutTimingCommand Body)> Requests { get; } = [];

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((idempotencyKey, (CheckpointWorkoutTimingCommand)(object)payload!));
            var snapshot = new ActiveWorkoutSessionSnapshot(sessionId, "Synthetic", DateTime.UnixEpoch,
                null, 0, 0m, null, null, false, false, []);
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)(object)snapshot));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SessionApi(BackendApiStatus status, string? code = null) : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(status, default, code));

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
