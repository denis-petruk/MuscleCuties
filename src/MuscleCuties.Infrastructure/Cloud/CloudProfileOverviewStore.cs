

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The profile tab's summary through the backend.</summary>
public sealed class CloudProfileOverviewStore(
    IMuscleCutiesBackendApiClient apiClient,
    MobileJourneyCloudGateway cloudGateway) : IProfileOverviewStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string PhotosRoute = "/api/v1/photos";
    private const string WorkoutHistoryCountRoute = "/api/v1/workouts/history/count";
    private const string ActiveInjuriesRoute = "/api/v1/injuries?activeOnly=true";
    private const string PhotosLatestRoute = "/api/v1/photos?limit=1&offset=0";
    private const string CycleOverviewRoute = "/api/v1/cycle/overview";
    private const int DefaultCycleLengthDays = 28;

#if DEBUG
    /// <summary>Sends the profile request and traces the response.</summary>
    private static async Task<BackendApiResponse<T>> TraceProfileResponseAsync<T>(
        Task<BackendApiResponse<T>> responseTask, string operation)
    {
        var response = await responseTask.ConfigureAwait(false);
        if (response.Status != BackendApiStatus.Success)
            System.Diagnostics.Trace.WriteLine($"[ProfileOverview] {operation}: {response.Status}");
        return response;
    }
#endif

    /// <summary>The summary figures, read as one payload so the tab does not show a half-updated set.</summary>
    public async Task<ProfileOverviewSource> LoadAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var profileTask = apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken);
        var dashboardTask = cloudGateway.GetDashboardOverviewAsync(today, cancellationToken);
        var cycleTask = apiClient.GetAsync<ServerCycleOverview>(
            $"{CycleOverviewRoute}?from={today:yyyy-MM-dd}&through={today:yyyy-MM-dd}&today={today:yyyy-MM-dd}",
            cancellationToken);
        var workoutCountTask = apiClient.GetAsync<WorkoutHistoryCountDto>(WorkoutHistoryCountRoute, cancellationToken);
        var injuriesTask = apiClient.GetAsync<InjuryObservationRecord[]>(ActiveInjuriesRoute, cancellationToken);
        var photosTask = apiClient.GetAsync<PhotoHistoryResponse>(PhotosLatestRoute, cancellationToken);

#if DEBUG
        profileTask = TraceProfileResponseAsync(profileTask, "Profile");
        dashboardTask = TraceProfileResponseAsync(dashboardTask, "Dashboard");
        cycleTask = TraceProfileResponseAsync(cycleTask, "Cycle");
        workoutCountTask = TraceProfileResponseAsync(workoutCountTask, "WorkoutCount");
        injuriesTask = TraceProfileResponseAsync(injuriesTask, "Injuries");
        photosTask = TraceProfileResponseAsync(photosTask, "Photos");
#endif
        await Task.WhenAll(profileTask, dashboardTask, cycleTask, workoutCountTask, injuriesTask, photosTask).ConfigureAwait(false);

        var profile = CloudStoreResponse.RequireData(await profileTask.ConfigureAwait(false), "profile overview profile");
        var dashboard = CloudStoreResponse.RequireData(await dashboardTask.ConfigureAwait(false), "profile overview dashboard");
        var cycleResponse = await cycleTask.ConfigureAwait(false);
        var completedSessions = CloudStoreResponse.RequireData(
            await workoutCountTask.ConfigureAwait(false), "profile overview workout count").Count;
        var activeInjuryCount = CloudStoreResponse.RequireData(
            await injuriesTask.ConfigureAwait(false), "profile overview injuries").Count(injury => injury.IsActive);
        var photosResponse = await photosTask.ConfigureAwait(false);
        var isVaultAvailable = photosResponse.Status == BackendApiStatus.Success && photosResponse.Data is not null;
        var vaultPhotoCount = isVaultAvailable ? photosResponse.Data!.TotalCount : -1;
        var latestVaultPhotoPath = string.Empty;
        if (isVaultAvailable && photosResponse.Data!.Photos.Count > 0)
        {
            var latestPhoto = photosResponse.Data!.Photos[0];
            var downloadResponse = await apiClient.GetAsync<PhotoDownloadUrlResponse>(
                $"{PhotosRoute}/{latestPhoto.Id:D}/download-url",
                cancellationToken).ConfigureAwait(false);
            if (downloadResponse.Status == BackendApiStatus.Success &&
                downloadResponse.Data is not null &&
                !string.IsNullOrWhiteSpace(downloadResponse.Data.PresignedDownloadUrl))
            {
                latestVaultPhotoPath = downloadResponse.Data.PresignedDownloadUrl;
            }
        }

        var profileImagePath = string.Empty;
        if (!string.IsNullOrWhiteSpace(profile?.ProfileImagePath))
        {
            if (Guid.TryParse(profile.ProfileImagePath, out var avatarPhotoId) && avatarPhotoId != Guid.Empty)
            {
                var avatarDownload = await apiClient.GetAsync<PhotoDownloadUrlResponse>(
                    $"{PhotosRoute}/{avatarPhotoId:D}/download-url",
                    cancellationToken).ConfigureAwait(false);
                if (avatarDownload.Status == BackendApiStatus.Success &&
                    avatarDownload.Data is not null &&
                    !string.IsNullOrWhiteSpace(avatarDownload.Data.PresignedDownloadUrl))
                {
                    profileImagePath = avatarDownload.Data.PresignedDownloadUrl;
                }
            }
        }

        var goal = profile is not null && Enum.TryParse<UserGoal>(profile.Goal, true, out var parsedGoal) &&
                   Enum.IsDefined(parsedGoal)
            ? parsedGoal
            : UserGoal.MaintainHealth;
        var pace = profile is not null && Enum.TryParse<WeightGoalPace>(profile.WeightGoalPace, true, out var parsedPace) &&
                   Enum.IsDefined(parsedPace)
            ? parsedPace
            : WeightGoalPace.Steady;
        var units = DisplayUnits.FromStored(
            profile?.UnitSystem,
            profile?.BodyWeightUnit,
            profile?.FoodMassUnit,
            profile?.HeightUnit,
            profile?.DistanceUnit,
            profile?.EnergyUnit);
        var predictedPhase = cycleResponse.Status == BackendApiStatus.Success &&
                             cycleResponse.Data is { } cycle &&
                             Enum.TryParse<CyclePhase>(cycle.Prediction.CurrentPhase, true, out var cyclePhase) &&
                             Enum.IsDefined(cyclePhase)
            ? cyclePhase
            : dashboard is not null &&
              Enum.TryParse<CyclePhase>(dashboard.CurrentPhase, true, out var dashboardPhase) &&
              Enum.IsDefined(dashboardPhase)
                ? dashboardPhase
                : CyclePhase.Unknown;
        var availableWeekdaysCount = profile is null
            ? 0
            : CloudWorkoutPreferencesSnapshotMapper.ToPreferences(profile).AvailableWeekdays.Count;

        var overview = new ProfileOverviewDto(
            Email: profile?.Email ?? string.Empty,
            MemberSince: profile?.CreatedAtUtc,
            Name: profile?.Name ?? dashboard?.DisplayName ?? string.Empty,
            Goal: goal,
            WeightGoalPace: pace,
            CycleDays: profile?.CycleLength is > 0 ? profile.CycleLength : (dashboard?.PredictedCycleLength is > 0 ? dashboard.PredictedCycleLength : DefaultCycleLengthDays),
            ProfileImagePath: profileImagePath,
            UnitSystem: units.UnitSystem,
            BodyWeightUnit: units.BodyWeightUnit,
            HeightUnit: units.HeightUnit,
            AvailableWeekdaysCount: availableWeekdaysCount,
            CompletedSessions: completedSessions,
            WorkoutStreakDays: dashboard?.WorkoutStreakDays ?? 0,
            NutritionStreakDays: dashboard?.NutritionStreakDays ?? 0,
            CurrentPhase: CycleContractMapper.ToDto(predictedPhase),
            CyclesTracked: dashboard?.CycleCount ?? 0,
            IsHealthConnected: null,
            ActiveInjuryCount: activeInjuryCount,
            VaultPhotoCount: vaultPhotoCount,
            LatestVaultPhotoPath: latestVaultPhotoPath);

        return new ProfileOverviewSource(overview, predictedPhase, predictedPhase);
    }

    /// <summary>Signs out from the profile tab.</summary>
    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await cloudGateway.LogoutAndPurgeLocalStateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stores the avatar path on the profile and returns it, or nothing when there was no image to store.</summary>
    public Task<string?> UpdateProfileImageAsync(string? imagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // A profile reference must only be set after a validated private-photo intent,
        // upload, commit, and first-party read URL have all succeeded.
        throw new NotSupportedException("Cloud profile image updates require a validated, account-scoped first-party upload contract.");
    }
}
