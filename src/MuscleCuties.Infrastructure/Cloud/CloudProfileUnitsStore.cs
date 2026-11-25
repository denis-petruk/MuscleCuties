

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The unit choices through the backend.</summary>
public sealed class CloudProfileUnitsStore(
    IMuscleCutiesBackendApiClient apiClient) : IProfileUnitsStore
{
    private const string ProfileUnitsRoute = "/api/v1/profile/units";
    private const int FallbackInitialVersion = 1;

    /// <summary>The unit choices in force, or nothing before they have been saved.</summary>
    public async Task<DisplayUnits?> LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<ServerProfileUnitsSnapshot>(ProfileUnitsRoute, cancellationToken).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.NotFound)
            return null;
        var units = CloudStoreResponse.RequireData(response, "profile units");

        return DisplayUnits.FromInput(
            units.UseMetricSystem,
            units.BodyWeightUnit,
            units.FoodMassUnit,
            units.HeightUnit,
            units.DistanceUnit,
            units.EnergyUnit);
    }

    /// <summary>Saves the unit preference after snapshotting the profile.</summary>
    public async Task<bool> SaveCurrentWithSnapshotAsync(
        DisplayUnits settings,
        string snapshotReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var currentUnits = await apiClient.GetAsync<ServerProfileUnitsSnapshot>(ProfileUnitsRoute, cancellationToken).ConfigureAwait(false);
        var expectedVersion = currentUnits.Status == BackendApiStatus.NotFound
            ? FallbackInitialVersion
            : CloudStoreResponse.RequireData(currentUnits, "profile units before save").Version;

        var response = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            ProfileUnitsRoute,
            new
            {
                expectedVersion,
                useMetricSystem = settings.UseMetricSystem,
                bodyWeightUnit = settings.BodyWeightUnit,
                foodMassUnit = settings.FoodMassUnit,
                heightUnit = settings.HeightUnit,
                distanceUnit = settings.DistanceUnit,
                energyUnit = settings.EnergyUnit
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success;
    }
}
