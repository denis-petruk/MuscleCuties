

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudProfileWorkoutPreferencesStore(
    IMuscleCutiesBackendApiClient apiClient) : IProfileWorkoutPreferencesStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string WorkoutPreferencesRoute = "/api/v1/profile/workout-preferences";

    public async Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound)
            return null;
        var snapshot = CloudStoreResponse.RequireData(response, "workout preferences profile");
        return CloudWorkoutPreferencesSnapshotMapper.ToPreferences(snapshot);
    }

    public async Task<bool> SaveAsync(WorkoutPreferencesDto preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var currentResponse = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken)
            .ConfigureAwait(false);
        if (currentResponse.Status != BackendApiStatus.Success || currentResponse.Data is null)
            return false;

        var response = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            WorkoutPreferencesRoute,
            new
            {
                expectedVersion = currentResponse.Data.Version,
                activityTypes = preferences.ActivityTypes,
                availableWeekdays = preferences.AvailableWeekdays,
                activitySessionDurationsMinutes = preferences.ActivitySessionDurationsMinutes,
                equipmentLevel = preferences.EquipmentLevel ?? currentResponse.Data.EquipmentLevel,
                requestedSessionsPerWeek = preferences.RequestedSessionsPerWeek ?? new Dictionary<int, int>(),
                excludedExerciseIds = preferences.ExcludedExerciseIds?.ToArray() ?? [],
                gluteFocus = preferences.GluteFocus,
                equipmentCapabilities = (preferences.HasSliders is not null ||
                    preferences.HasConfirmedBandAnchor is not null ||
                    preferences.HasDumbbells is not null ||
                    preferences.HasBands is not null)
                    ? new
                    {
                        hasSliders = preferences.HasSliders,
                        hasConfirmedBandAnchor = preferences.HasConfirmedBandAnchor,
                        hasDumbbells = preferences.HasDumbbells,
                        hasBands = preferences.HasBands
                    }
                    : null
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);
        return response.Status == BackendApiStatus.Success;
    }
}
