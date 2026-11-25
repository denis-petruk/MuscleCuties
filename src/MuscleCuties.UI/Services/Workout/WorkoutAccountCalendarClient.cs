using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.Services.Workout;

/// <summary>Keeps the server's workout calendar in the device's time zone.</summary>
public interface IWorkoutAccountCalendarClient
{
    Task UpdateForegroundTimeZoneAsync(CancellationToken cancellationToken = default);
}

/// <summary>Sends the device time zone to the server when it differs, gated so two calls cannot race the</summary>
/// same version.</summary>
public sealed class WorkoutAccountCalendarClient(IMuscleCutiesBackendApiClient apiClient)
    : IWorkoutAccountCalendarClient, IDisposable
{
    private const string ProgressRoute = "/api/v1/workouts/progress";
    private const string ForegroundTimeZoneRoute = "/api/v1/workouts/foreground-timezone";
    private const string ReadyStatus = "Ready";
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    /// <summary>Releases the client's update gate.</summary>
    public void Dispose() => _updateGate.Dispose();

    /// <summary>Reads the calendar state and, when the stored zone differs from the device's, sends the change</summary>
    /// with the version it just read. A conflict is surfaced so the caller reloads instead of retrying blind.</summary>
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
