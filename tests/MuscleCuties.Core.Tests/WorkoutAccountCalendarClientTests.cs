using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutAccountCalendarClientTests
{
    private const long CurrentVersion = 7;

    [Fact]
    public async Task ChangedTimeZoneUsesObservedVersionAndForegroundFlag()
    {
        var api = new CalendarApi("Synthetic/Different", BackendApiStatus.Success);
        var client = new WorkoutAccountCalendarClient(api);

        await client.UpdateForegroundTimeZoneAsync();

        Assert.NotNull(api.Sent);
        Assert.Equal(CurrentVersion, api.Sent.ExpectedVersion);
        Assert.Equal(TimeZoneInfo.Local.Id, api.Sent.TimeZoneId);
        Assert.True(api.Sent.IsForeground);
    }

    [Fact]
    public async Task UnchangedTimeZoneDoesNotWrite()
    {
        var api = new CalendarApi(TimeZoneInfo.Local.Id, BackendApiStatus.Success);
        await new WorkoutAccountCalendarClient(api).UpdateForegroundTimeZoneAsync();
        Assert.Null(api.Sent);
    }

    [Fact]
    public async Task StaleVersionIsNotRetriedAsAnUnconditionalWrite()
    {
        var api = new CalendarApi("Synthetic/Different", BackendApiStatus.Conflict);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WorkoutAccountCalendarClient(api).UpdateForegroundTimeZoneAsync());
        Assert.Equal(1, api.WriteCount);
        Assert.Equal(CurrentVersion, api.Sent!.ExpectedVersion);
    }

    private sealed class CalendarApi(string? timeZoneId, BackendApiStatus writeStatus) : IMuscleCutiesBackendApiClient
    {
        public WorkoutForegroundTimeZoneRequest? Sent { get; private set; }
        public int WriteCount { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            var value = new WorkoutProgressResponse("Ready", timeZoneId, CurrentVersion, null, 0, 0, false);
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)(object)value));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            Sent = (WorkoutForegroundTimeZoneRequest)(object)payload!;
            WriteCount++;
            var value = new WorkoutProgressResponse("Ready", Sent.TimeZoneId, CurrentVersion + 1, null, 0, 0, false);
            return Task.FromResult(new BackendApiResponse<TResponse>(writeStatus,
                writeStatus == BackendApiStatus.Success ? (TResponse)(object)value : default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
