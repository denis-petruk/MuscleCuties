using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.Services.Workout;

public interface IWorkoutAccountCalendarClient
{
    Task UpdateForegroundTimeZoneAsync(CancellationToken cancellationToken = default);
}

public sealed class WorkoutAccountCalendarClient(IMuscleCutiesBackendApiClient apiClient)
    : IWorkoutAccountCalendarClient
{
    private const string ProgressRoute = "/api/v1/workouts/progress";
    private const string ForegroundTimeZoneRoute = "/api/v1/workouts/foreground-timezone";
    private const string ReadyStatus = "Ready";
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    public async Task UpdateForegroundTimeZoneAsync(CancellationToken cancellationToken = default)
    {
        await _updateGate.WaitAsync(cancellationToken);
        try
        {
        var progress = await apiClient.GetAsync<WorkoutProgressResponse>(ProgressRoute, cancellationToken);
        if (progress.Status == BackendApiStatus.Unauthorized)
            throw new UnauthorizedAccessException("Workout calendar session was rejected.");
        if (progress.Status != BackendApiStatus.Success || progress.Data is null)
            throw new HttpRequestException("Could not load workout calendar state.");
        if (string.Equals(progress.Data.TimeZoneId, TimeZoneInfo.Local.Id, StringComparison.Ordinal))
            return;

        var request = new WorkoutForegroundTimeZoneRequest(TimeZoneInfo.Local.Id, progress.Data.TimeZoneVersion, true);
        var result = await apiClient.SendMutationAsync<WorkoutForegroundTimeZoneRequest, WorkoutProgressResponse>(
            HttpMethod.Put, ForegroundTimeZoneRoute, request, Guid.NewGuid().ToString("N"), cancellationToken);
        if (result.Status == BackendApiStatus.Unauthorized)
            throw new UnauthorizedAccessException("Workout calendar session was rejected.");
        if (result.Status == BackendApiStatus.Conflict)
            throw new InvalidOperationException("Workout calendar state changed. Reload before creating a preview.");
        if (result.Status != BackendApiStatus.Success || result.Data?.Status != ReadyStatus)
            throw new HttpRequestException("Could not update workout calendar state.");
        }
        finally
        {
            _updateGate.Release();
        }
    }
}
