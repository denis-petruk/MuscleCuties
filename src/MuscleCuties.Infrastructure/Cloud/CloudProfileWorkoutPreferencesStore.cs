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
