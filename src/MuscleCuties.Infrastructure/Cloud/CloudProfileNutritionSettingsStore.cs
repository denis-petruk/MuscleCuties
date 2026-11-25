

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The nutrition settings page's data through the backend.</summary>
public sealed class CloudProfileNutritionSettingsStore(
    IMuscleCutiesBackendApiClient apiClient,
    INutritionPlanner? planner = null) : IProfileNutritionSettingsStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string NutritionPreferencesRoute = "/api/v1/profile/nutrition-preferences";
    private const string NutritionTargetsRoute = "/api/v1/profile/nutrition-targets";
    private const string CycleOverviewRoute = "/api/v1/cycle/overview";

    /// <summary>The saved nutrition settings, or nothing before they have ever been saved.</summary>
    public async Task<NutritionSettingsProfileDto?> LoadAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var profileTask = apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken);
        var cycleTask = apiClient.GetAsync<ServerCycleOverview>(
            $"{CycleOverviewRoute}?from={today:yyyy-MM-dd}&through={today:yyyy-MM-dd}&today={today:yyyy-MM-dd}",
            cancellationToken);

        await Task.WhenAll(profileTask, cycleTask).ConfigureAwait(false);

        var profileResponse = await profileTask.ConfigureAwait(false);
        if (profileResponse.Status == BackendApiStatus.NotFound)
            return null;
        var snapshot = CloudStoreResponse.RequireData(profileResponse, "nutrition settings profile");
        var cycleResponse = await cycleTask.ConfigureAwait(false);
        var cycle = CloudStoreResponse.RequireData(cycleResponse, "nutrition settings cycle");
        var phase = Enum.TryParse<CyclePhase>(cycle.Prediction.CurrentPhase, true, out var parsedPhase) &&
                    Enum.IsDefined(parsedPhase)
            ? parsedPhase
            : CyclePhase.Unknown;

        // S-02: a missing date of birth stays missing, exactly as the personal-info store returns it.
        var dob = snapshot.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? default;
        var goal = Enum.TryParse<UserGoal>(snapshot.Goal, true, out var g) && Enum.IsDefined(g)
            ? g
            : UserGoal.MaintainHealth;
        var pace = Enum.TryParse<WeightGoalPace>(snapshot.WeightGoalPace, true, out var p) && Enum.IsDefined(p)
            ? p
            : WeightGoalPace.Steady;
        var experience = Enum.TryParse<TrainingExperienceLevel>(snapshot.TrainingExperienceLevel, true, out var exp) &&
                         Enum.IsDefined(exp)
            ? exp
            : TrainingExperienceLevel.Intermediate;
        var savedGoals = NutritionGoalsDtoMapper.ToDto(ProfileNutritionGoalsJsonCodec.FromJson(snapshot.NutritionGoalsJson));

        return new NutritionSettingsProfileDto(
            dob,
            (float)snapshot.Height,
            (float)snapshot.Weight,
            experience,
            snapshot.WorkoutDaysPerWeek,
            goal,
            pace,
            snapshot.DietaryTags ?? string.Empty,
            savedGoals,
            phase);
    }

    /// <summary>Calculates the nutrition goals for the profile.</summary>
    public NutritionGoalsDto CalculateGoals(
        NutritionSettingsProfileDto profile, NutritionSettingsSelectionDto selection)
    {
        if (planner is null)
            return NutritionGoalsDto.Empty;

        var planningProfile = new UserProfile
        {
            Name = string.Empty,
            DateOfBirth = profile.DateOfBirth,
            Height = profile.Height,
            Weight = profile.Weight,
            TrainingExperienceLevel = profile.TrainingExperienceLevel,
            WorkoutDaysPerWeek = profile.WorkoutDaysPerWeek,
            Goal = selection.Goal,
            WeightGoalPace = selection.WeightGoalPace,
            DietaryTags = selection.DietaryTags,
            NutritionGoalsJson = string.Empty
        };

        var plan = planner.CreateDailyPlan(planningProfile, profile.Phase, DateTime.Today);
        return NutritionGoalsDtoMapper.ToDto(plan.Goals);
    }

    /// <summary>Saves the nutrition settings.</summary>
    public async Task<bool> SaveAsync(
        NutritionSettingsSelectionDto selection, NutritionGoalsDto? goalOverrides, CancellationToken cancellationToken)
    {
        var currentResponse = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        if (currentResponse.Status != BackendApiStatus.Success || currentResponse.Data is null)
            return false;

        var prefResponse = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            NutritionPreferencesRoute,
            new
            {
                expectedVersion = currentResponse.Data.Version,
                goal = selection.Goal.ToString(),
                weightGoalPace = selection.WeightGoalPace.ToString(),
                dietaryTags = selection.DietaryTags ?? string.Empty
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        if (prefResponse.Status != BackendApiStatus.Success)
            return false;

        if (goalOverrides is not null)
        {
            var nextVersion = prefResponse.Data?.Version ?? (currentResponse.Data.Version + 1);
            var domainGoals = NutritionGoalsDtoMapper.ToDomain(goalOverrides);
            var serializedGoals = ProfileNutritionGoalsJsonCodec.ToJson(domainGoals);
            using var doc = JsonDocument.Parse(serializedGoals);
            var targets = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
                targets[prop.Name] = prop.Value.Clone();

            var targetsResponse = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
                HttpMethod.Put,
                NutritionTargetsRoute,
                new
                {
                    expectedVersion = nextVersion,
                    targets
                },
                idempotencyKey: Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);

            if (targetsResponse.Status != BackendApiStatus.Success)
                return false;
        }

        return true;
    }
}
