using System.Collections.Concurrent;
using System.Text.Json;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Nutrition.Planning;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;
using MuscleCuties.UseCases.Features.Account.Otp;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;
using MuscleCuties.UseCases.Features.Account.Recovery;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;
using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;
using MuscleCuties.UseCases.Features.Profile.Overview;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Archive;


namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudProfileUnitsStore(
    IMuscleCutiesBackendApiClient apiClient) : IProfileUnitsStore
{
    private const string ProfileUnitsRoute = "/api/v1/profile/units";
    private const int FallbackInitialVersion = 1;

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
