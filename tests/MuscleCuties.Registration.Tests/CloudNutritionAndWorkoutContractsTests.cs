using System.Text.Json;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.Infrastructure.Cycle.Overview;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;
using MuscleCuties.UseCases.Features.Cycle.History;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.Registration.Tests;

public sealed class CloudNutritionAndWorkoutContractsTests
{
    private const int InitialProfileVersion = 1;
    private const int RefreshedProfileVersion = 2;
    private const float LegacyIngredientGrams = 120f;
    private const float LegacyIngredientCaloriesPer100Grams = 107.5f;
    private const float LegacyIngredientProteinPer100Grams = 6.1f;
    private const float LegacyIngredientCarbsPer100Grams = 18.2f;
    private const float LegacyIngredientFatsPer100Grams = 1.2f;
    private const float LegacyOilGrams = 12f;
    private const float LegacyOilCaloriesPer100Grams = 900f;
    private const float MealTargetCalories = 600f;
    private const float MealTargetFats = 20f;
    private const float MealTargetProtein = 40f;
    private const float MealTargetCarbs = 70f;
    private const float DailyTargetCalories = 2000f;
    private const float MacroTolerance = 0.001f;
    private const float NutrientReferenceGrams = 100f;
    private const float ReferenceOilCaloriesPer100Grams = 884f;

    [Xunit.Fact]
    public async Task CloudGeneratedMealsRepairLegacyEqualSplitAcrossEveryResponseRoute()
    {
        var legacyComponents = new[] { "Tofu", "Edamame", "Broccoli", "Jasmine Rice" }
            .Select(name => new MealSuggestionComponentDto(0, name, "Ingredient", "Base",
                LegacyIngredientGrams, false,
                LegacyIngredientCaloriesPer100Grams,
                LegacyIngredientProteinPer100Grams,
                LegacyIngredientCarbsPer100Grams,
                LegacyIngredientFatsPer100Grams))
            .Append(new MealSuggestionComponentDto(0, "Sesame Oil", "Sauce", "SauceOnSide",
                LegacyOilGrams, false, LegacyOilCaloriesPer100Grams, 0f, 0f, 100f))
            .ToArray();
        var legacy = new MealSuggestionDto("Tofu Stir-Fry with Edamame", string.Empty, [],
            legacyComponents,
            [new MealSuggestionComponentGroupDto("Ingredients", "Food24", legacyComponents)],
            MealTargetCalories, MealTargetProtein, MealTargetCarbs, MealTargetFats,
            1f, "Balanced", "Food24", string.Empty);
        var legacyPlan = new DayMealPlanItemDto("Dinner", MealTargetCalories, MealTargetProtein,
            MealTargetCarbs, MealTargetFats, DailyTargetCalories, false, legacy);
        var apiClient = new RecordingApiClient(path => path switch
        {
            var value when value.StartsWith("/api/v1/nutrition/suggestions?", StringComparison.Ordinal) =>
                new List<MealSuggestionDto> { legacy },
            var value when value.StartsWith("/api/v1/nutrition/meal-plan?", StringComparison.Ordinal) =>
                new List<DayMealPlanItemDto> { legacyPlan },
            _ => legacyPlan
        });
        var store = new CloudNutritionOverviewStore(apiClient);

        var suggestion = Xunit.Assert.Single(await store.GetMealSuggestionsAsync("Dinner", "Savoury", null));
        var planned = Xunit.Assert.Single(await store.GetDayMealPlanAsync("Savoury"));
        var refreshed = await store.RefreshDayMealAsync(legacyPlan, "Savoury");

        AssertRepaired(suggestion);
        AssertRepaired(Xunit.Assert.IsType<MealSuggestionDto>(planned.Suggestion));
        AssertRepaired(Xunit.Assert.IsType<MealSuggestionDto>(refreshed.Suggestion));
    }

    private static void AssertRepaired(MealSuggestionDto suggestion)
    {
        var components = suggestion.Components.ToDictionary(component => component.Name);
        Xunit.Assert.NotEqual(components["Tofu"].Grams, components["Edamame"].Grams);
        Xunit.Assert.NotEqual(components["Edamame"].Grams, components["Broccoli"].Grams);
        Xunit.Assert.NotEqual(components["Broccoli"].Calories, components["Jasmine Rice"].Calories);
        Xunit.Assert.Equal(ReferenceOilCaloriesPer100Grams, components["Sesame Oil"].Calories);
        Xunit.Assert.Equal(components["Tofu"].Grams,
            suggestion.ComponentGroups[0].Items.Single(item => item.Name == "Tofu").Grams);
        Xunit.Assert.InRange(suggestion.Calories,
            MealTargetCalories - MacroTolerance, MealTargetCalories + MacroTolerance);
        Xunit.Assert.InRange(suggestion.Protein - suggestion.Components.Sum(component =>
                component.Protein * component.Grams / NutrientReferenceGrams),
            -MacroTolerance, MacroTolerance);
    }

    [Xunit.Fact]
    public async Task CloudNutritionOverviewStoreCallsGroupedMealAndCustomFoodRoutes()
    {
        var apiClient = new RecordingApiClient(path =>
        {
            if (path == "/api/v1/nutrition/custom-foods")
            {
                return new CustomFoodResultDto(
                    -42,
                    "Skyr Berry Bowl",
                    120f,
                    15f,
                    10f,
                    2f,
                    "Custom food",
                    100f,
                    "g",
                    null,
                    "120 kcal");
            }

            if (path == "/api/v1/nutrition/meals/77/ingredients")
            {
                return new List<MealIngredientInputDto>
                {
                    new(-42, "Skyr Berry Bowl", 150f, 150f, "g", 120f, 15f, 10f, 2f, "Custom")
                };
            }

            return new object();
        });

        var store = new CloudNutritionOverviewStore(apiClient);
        var today = new DateOnly(2026, 10, 19);

        var created = await store.CreateCustomFoodAsync(
            new CreateCustomFoodRequest("Skyr Berry Bowl", 100f, "g", 120f, 15f, 10f, 2f));
        Xunit.Assert.Equal(-42, created.FoodItemId);

        await store.LogMealAsync(new LogMealRequest(
            today,
            "Breakfast",
            TimeSpan.FromHours(8),
            [new MealIngredientInputDto(-42, "Skyr Berry Bowl", 150f, 150f, "g", 120f, 15f, 10f, 2f, "Custom")]));

        await store.UpdateMealAsync(new UpdateMealRequest(
            77,
            today,
            "Lunch",
            TimeSpan.FromHours(13),
            [new MealIngredientInputDto(-42, "Skyr Berry Bowl", 150f, 150f, "g", 120f, 15f, 10f, 2f, "Custom")]));

        var ingredients = await store.GetMealIngredientsAsync(77);
        var single = Xunit.Assert.Single(ingredients);
        Xunit.Assert.Equal(-42, single.FoodItemId);

        Xunit.Assert.Contains(("POST", "/api/v1/nutrition/custom-foods"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("POST", "/api/v1/nutrition/meals"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("PUT", "/api/v1/nutrition/meals/77"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("GET", "/api/v1/nutrition/meals/77/ingredients"), apiClient.RecordedCalls);
    }

    [Xunit.Fact]
    public async Task WorkoutPreferenceSaveRefreshesProfileVersionAfterConcurrentProfileUpdate()
    {
        var apiClient = new ConflictThenSuccessApiClient();
        var store = new CloudProfileWorkoutPreferencesStore(apiClient);

        var result = await store.SaveAsync(CreateAllSelectedWorkoutPreferences(), CancellationToken.None);

        Xunit.Assert.Equal(WorkoutPreferencesError.None, result);
        Xunit.Assert.Equal(new[] { InitialProfileVersion, RefreshedProfileVersion }, apiClient.ExpectedVersions);
        Xunit.Assert.Equal(2, apiClient.ProfileReads);
        Xunit.Assert.Equal(2, apiClient.PreferenceWrites);
    }

    [Xunit.Theory]
    [Xunit.InlineData(BackendApiStatus.OfflineRejected, "", WorkoutPreferencesError.Offline)]
    [Xunit.InlineData(BackendApiStatus.Unauthorized, "", WorkoutPreferencesError.SignInRequired)]
    [Xunit.InlineData(BackendApiStatus.ServerError, "", WorkoutPreferencesError.ServerUnavailable)]
    [Xunit.InlineData(BackendApiStatus.Conflict, "", WorkoutPreferencesError.ConcurrentUpdate)]
    [Xunit.InlineData(BackendApiStatus.ValidationFailed, "invalidCatalogReference",
        WorkoutPreferencesError.ExerciseCatalogChanged)]
    public async Task WorkoutPreferenceSavePreservesActionableServerFailure(
        BackendApiStatus status,
        string errorCode,
        WorkoutPreferencesError expectedError)
    {
        var store = new CloudProfileWorkoutPreferencesStore(new FailedPreferenceWriteApiClient(status, errorCode));

        var result = await store.SaveAsync(CreateAllSelectedWorkoutPreferences(), CancellationToken.None);

        Xunit.Assert.Equal(expectedError, result);
    }

    [Xunit.Fact]
    public async Task CloudWorkoutStoresCallFirstPartyOverviewAndSessionRoutes()
    {
        var apiClient = new RecordingApiClient(path =>
        {
            if (path.StartsWith("/api/v1/workouts/overview", StringComparison.Ordinal))
            {
                return new WorkoutOverviewResponse(
                    "Personalized Follicular Plan",
                    "Follicular",
                    [new WorkoutListEntry(101, "STRENGTH", "MON", "Lower Sculpt", "45 min", "Hip Thrust", false, false, 4, 1)],
                    [],
                    []);
            }

            if (path == "/api/v1/workouts/day-sessions/101")
            {
                return new WorkoutSessionDetailDto(
                    101,
                    "Lower Sculpt",
                    "Strength work",
                    "4 exercises, about 45 min",
                    false,
                    [new WorkoutExerciseDetailDto(501, 12, "Hip Thrust", "STRENGTH", false, [])],
                    []);
            }

            if (path == "/api/v1/workouts/day-exercises/501/swaps")
            {
                return new List<WorkoutSwapCandidateDto>
                {
                    new(19, "Glute Bridge", "92% muscle match", "Bodyweight", "Glutes")
                };
            }

            if (path == "/api/v1/workouts/day-exercises/501/movement-note")
            {
                return new WorkoutMovementNoteDto(501, "Pause at top.");
            }

            return new object();
        });

        var overviewStore = new CloudWorkoutOverviewStore(apiClient);
        var sessionStore = new CloudWorkoutSessionActionStore(apiClient);
        var today = new DateOnly(2026, 10, 19);

        var overview = await overviewStore.LoadAsync(new WorkoutOverviewRequest(today), CancellationToken.None);
        Xunit.Assert.Equal("Personalized Follicular Plan", overview.PlanName);

        await overviewStore.RescheduleAsync(new RescheduleWorkoutDayRequest(101, today.AddDays(1)), CancellationToken.None);

        var detail = await sessionStore.GetDetailAsync(101, CancellationToken.None);
        Xunit.Assert.Equal(101, detail.WorkoutDayId);

        await sessionStore.LogAsync(
            new LogWorkoutSessionRequest(101, today, [new WorkoutExerciseLogDto(501, 12, 3, 10, 60f)]),
            CancellationToken.None);

        var swaps = await sessionStore.GetSwapCandidatesAsync(501, CancellationToken.None);
        Xunit.Assert.Single(swaps);

        await sessionStore.SwapAsync(new SwapWorkoutExerciseRequest(501, 19, true), CancellationToken.None);
        await sessionStore.SaveMovementNoteAsync(new SaveWorkoutMovementNoteRequest(501, "Pause at top."), CancellationToken.None);
        var note = await sessionStore.GetMovementNoteAsync(501, CancellationToken.None);
        Xunit.Assert.Equal("Pause at top.", note.Notes);

        Xunit.Assert.Contains(("GET", "/api/v1/workouts/overview?startDate=2026-10-19"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("POST", "/api/v1/workouts/reschedule"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("GET", "/api/v1/workouts/day-sessions/101"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("POST", "/api/v1/workouts/day-sessions/log"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("GET", "/api/v1/workouts/day-exercises/501/swaps"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("POST", "/api/v1/workouts/day-exercises/swap"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("PUT", "/api/v1/workouts/day-exercises/movement-note"), apiClient.RecordedCalls);
        Xunit.Assert.Contains(("GET", "/api/v1/workouts/day-exercises/501/movement-note"), apiClient.RecordedCalls);
    }

    [Xunit.Fact]
    public async Task CloudCheckInInjuryAndCycleStoresMapServerPayloadsAndRoutes()
    {
        var today = new DateOnly(2026, 10, 19);
        var nowUtc = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var existingCheckInId = Guid.NewGuid();
        var injuryGuid = Guid.NewGuid();
        var cycleGuid = Guid.NewGuid();

        var apiClient = new RecordingApiClient(path =>
        {
            if (path.StartsWith("/api/v1/check-ins", StringComparison.Ordinal))
            {
                var record = new DailyCheckInRecord(
                    existingCheckInId, today, 8.0m, 7.8m, 9500, 9000, 4, 1, false, 62.5m, null, null, null,
                    91, "High", "Ovulatory", 5, 1, 1, "UTC", nowUtc, 1);
                return path == "/api/v1/check-ins" ? (object)record : new[] { record };
            }

            if (path.StartsWith("/api/v1/workouts/overview", StringComparison.Ordinal))
            {
                return new WorkoutOverviewResponse(
                    "Ovulatory Strength Plan",
                    "Ovulatory",
                    [new WorkoutListEntry(101, "STRENGTH", "MON", "Glute & Hamstring Focus", "45 min", "Hip Thrust", false, true, 4, 1)],
                    [],
                    []);
            }

            if (path == "/api/v1/injuries")
            {
                var injury = new InjuryObservationRecord(injuryGuid, (int)WorkoutInjurySite.Shoulder, "Acute", today.AddDays(-3), 3, today, true);
                return new[] { injury };
            }

            if (path.StartsWith("/api/v1/cycle/overview", StringComparison.Ordinal))
            {
                return new ServerCycleOverview(
                    new CyclePredictionProjection(today, true, today.AddDays(-2), 3, 28, "Menstrual", today.AddDays(26), 26, today.AddDays(12), today.AddDays(8), today.AddDays(13), "recent cycles"),
                    new CycleHistorySnapshot(
                        [new CycleObservation(cycleGuid, today, null, 28)],
                        [new CycleSymptom(Guid.NewGuid(), cycleGuid, today, "Cramps", 2, "Mild")]),
                    [new ServerCyclePhaseLog(Guid.NewGuid(), cycleGuid, "Menstrual", "cycle.phase.menstrual", true, "Light", "cycle.flow.light", today.ToDateTime(TimeOnly.MinValue), "Period started", DateTime.UtcNow)],
                    "cycle.phase.menstrual",
                    "cycle.prediction_source.recent_cycles");
            }

            if (path == "/api/v1/cycle/history/phases")
                return JsonSerializer.SerializeToElement(new { outcome = "Saved" });

            if (path.StartsWith("/api/v1/cycle/history", StringComparison.Ordinal))
            {
                return new CycleHistorySnapshot(
                    [new CycleObservation(cycleGuid, today, null, 28)],
                    []);
            }

            return new JsonElement();
        });

        var checkInStore = new CloudDailyCheckInStore(apiClient, clock: new FixedTimeProvider(nowUtc));
        var status = await checkInStore.GetStatusAsync(new DailyCheckInStatusRequest(today, "UTC"), CancellationToken.None);
        Xunit.Assert.Equal("Available", status.TodayReadiness?.Status);
        Xunit.Assert.True(status.IsCompleted);
        Xunit.Assert.Equal("93.75", status.ScoreText);
        Xunit.Assert.Equal("FeelingReady", status.TierText);

        var injuryStore = new CloudWorkoutInjuryStore(apiClient);
        var injuries = await injuryStore.GetAllInjuriesAsync(CancellationToken.None);
        var singleInjury = Xunit.Assert.Single(injuries);
        Xunit.Assert.Equal(WorkoutInjurySite.Shoulder, singleInjury.Site);
        Xunit.Assert.Equal(WorkoutInjuryStatus.Acute, singleInjury.Status);
        Xunit.Assert.Equal(3, singleInjury.Pain);

        await injuryStore.DeleteInjuryAsync(singleInjury.Id, CancellationToken.None);
        Xunit.Assert.Contains(("DELETE", $"/api/v1/injuries/{injuryGuid:D}"), apiClient.RecordedCalls);

        var dailyCycleStore = new CloudCycleDailyLogStore(apiClient);
        var loadedCycleLog = await dailyCycleStore.LoadAsync(today.ToDateTime(TimeOnly.MinValue), CancellationToken.None);
        Xunit.Assert.NotNull(loadedCycleLog);
        Xunit.Assert.True(loadedCycleLog!.IsPeriodStarted);
        Xunit.Assert.Equal(FlowIntensity.Light, loadedCycleLog.FlowIntensity);
        Xunit.Assert.Contains(SymptomType.Cramps, loadedCycleLog.Symptoms);

        var phaseEditStore = new CloudCyclePhaseEditStore(apiClient);
        var savedPhase = await phaseEditStore.SaveAsync(
            new CyclePhaseEditRequest(today.ToDateTime(TimeOnly.MinValue), CyclePhase.Ovulatory, "Calendar phase edit"),
            CancellationToken.None);
        Xunit.Assert.True(savedPhase);
        Xunit.Assert.Contains(("PUT", "/api/v1/cycle/history/phases"), apiClient.RecordedCalls);
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    [Xunit.Fact]
    public async Task CloudProfileStoresUseVersionedServerContractsAndRoutes()
    {
        var apiClient = new RecordingApiClient(path =>
        {
            if (path == "/api/v1/profile")
            {
                return new ServerProfileSnapshot(
                    Name: "Elena",
                    DateOfBirth: new DateOnly(1998, 5, 14),
                    Height: 168m,
                    Weight: 61.5m,
                    Goal: "Strength",
                    WeightGoalPace: "Steady",
                    TrainingExperienceLevel: "Advanced",
                    WorkoutDaysPerWeek: 4,
                    AvailableWorkoutWeekdaysJson: "[1,3,5,6]",
                    CycleLength: 29,
                    DietaryTags: "Vegetarian",
                    ActivitySessionDurationsJson: "{\"HighVolumeStrength\":60,\"Yoga\":15}",
                    EquipmentLevel: "FullGym",
                    PreferredWorkoutActivityTypes: "HighVolumeStrength,Yoga",
                    UnitSystem: "Metric",
                    BodyWeightUnit: "kg",
                    FoodMassUnit: "g",
                    HeightUnit: "cm",
                    DistanceUnit: "km",
                    EnergyUnit: "kcal",
                    NutritionGoalsJson: "{}",
                    Version: 3,
                    Email: "elena@example.test",
                    ReproductiveContextJson: "{\"Pregnancy\":1,\"Contraception\":2}",
                    RequestedActivitySessionsPerWeekJson: "{\"Yoga\":2}");
            }

            if (path.StartsWith("/api/v1/cycle/overview", StringComparison.Ordinal))
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                return new ServerCycleOverview(
                    new CyclePredictionProjection(today, true, today.AddDays(-10), 11, 29, "Follicular", today.AddDays(19), 19, today.AddDays(4), today, today.AddDays(5), "profile"),
                    new CycleHistorySnapshot([], []),
                    [],
                    "cycle.phase.follicular",
                    "cycle.prediction_source.profile");
            }

            return new CloudProfileWriteResponse(4);
        });

        var personalStore = new CloudProfilePersonalInfoStore(apiClient);
        var loadedPersonal = await personalStore.LoadAsync(CancellationToken.None);
        Xunit.Assert.Equal("elena@example.test", loadedPersonal.Email);
        Xunit.Assert.NotNull(loadedPersonal.Profile);
        Xunit.Assert.Equal("Elena", loadedPersonal.Profile!.Name);
        Xunit.Assert.Equal(168f, loadedPersonal.Profile.Height);
        Xunit.Assert.Equal(61.5f, loadedPersonal.Profile.Weight);
        Xunit.Assert.Equal((int)UserGoal.Strength, loadedPersonal.Profile.Goal);
        Xunit.Assert.Equal((int)TrainingExperienceLevel.Advanced, loadedPersonal.Profile.TrainingExperienceLevel);
        Xunit.Assert.Equal(29, loadedPersonal.Profile.CycleLength);
        Xunit.Assert.Equal(1, loadedPersonal.Profile.PregnancyStatus);
        Xunit.Assert.Equal(2, loadedPersonal.Profile.ContraceptionStatus);

        var savedPersonal = await personalStore.SaveWithSnapshotAsync(
            new SavePersonalInfoRequest(
                "elena@example.test", "Elena", new DateTime(1998, 5, 14), 168f, 62f,
                (int)UserGoal.Strength, (int)WeightGoalPace.Steady, (int)TrainingExperienceLevel.Advanced,
                29, 1, 2),
            CancellationToken.None);
        Xunit.Assert.True(savedPersonal);
        Xunit.Assert.Contains(("PUT", "/api/v1/profile"), apiClient.RecordedCalls);

        var nutritionSettingsStore = new CloudProfileNutritionSettingsStore(apiClient);
        var savedNutrition = await nutritionSettingsStore.SaveAsync(
            new NutritionSettingsSelectionDto(UserGoal.Strength, WeightGoalPace.Steady, "Vegetarian"),
            null,
            CancellationToken.None);
        Xunit.Assert.True(savedNutrition);
        Xunit.Assert.Contains(("PUT", "/api/v1/profile/nutrition-preferences"), apiClient.RecordedCalls);

        var workoutPrefsStore = new CloudProfileWorkoutPreferencesStore(apiClient);
        var loadedWorkoutPrefs = await workoutPrefsStore.LoadAsync(CancellationToken.None);
        Xunit.Assert.NotNull(loadedWorkoutPrefs);
        Xunit.Assert.Equal(new[] { 1, 3, 5, 6 }, loadedWorkoutPrefs!.AvailableWeekdays);
        var savedWorkoutPrefs = await workoutPrefsStore.SaveAsync(loadedWorkoutPrefs, CancellationToken.None);
        Xunit.Assert.Equal(WorkoutPreferencesError.None, savedWorkoutPrefs);
        Xunit.Assert.Contains(("PUT", "/api/v1/profile/workout-preferences"), apiClient.RecordedCalls);
    }

    private sealed class RecordingApiClient(Func<string, object> responder) : IMuscleCutiesBackendApiClient
    {
        public List<(string Method, string Path)> RecordedCalls { get; } = [];

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            RecordedCalls.Add(("GET", relativePath));
            var result = (TResponse)responder(relativePath);
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, result));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            RecordedCalls.Add((method.Method, relativePath));
            var raw = responder(relativePath);
            var result = raw is TResponse typed ? typed :
                raw is JsonElement json && json.ValueKind != JsonValueKind.Undefined
                    ? json.Deserialize<TResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    : default;
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, result));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            RecordedCalls.Add(("DELETE", relativePath));
            return Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
        }
    }

    private sealed class ConflictThenSuccessApiClient : IMuscleCutiesBackendApiClient
    {
        public List<int> ExpectedVersions { get; } = [];
        public int ProfileReads { get; private set; }
        public int PreferenceWrites { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            Xunit.Assert.Equal("/api/v1/profile", relativePath);
            ProfileReads++;
            var snapshot = new ServerProfileSnapshot(
                Name: "Synthetic",
                DateOfBirth: null,
                Height: decimal.Zero,
                Weight: decimal.Zero,
                Goal: "Strength",
                WeightGoalPace: "Steady",
                TrainingExperienceLevel: "Beginner",
                WorkoutDaysPerWeek: 0,
                AvailableWorkoutWeekdaysJson: "[]",
                CycleLength: 0,
                DietaryTags: string.Empty,
                ActivitySessionDurationsJson: "{}",
                EquipmentLevel: Equipment.FullGym.ToString(),
                PreferredWorkoutActivityTypes: string.Empty,
                UnitSystem: "Metric",
                BodyWeightUnit: "kg",
                FoodMassUnit: "g",
                HeightUnit: "cm",
                DistanceUnit: "km",
                EnergyUnit: "kcal",
                NutritionGoalsJson: "{}",
                Version: ProfileReads == 1 ? InitialProfileVersion : RefreshedProfileVersion);
            return Task.FromResult(new BackendApiResponse<TResponse>(
                BackendApiStatus.Success, (TResponse)(object)snapshot));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Xunit.Assert.Equal(HttpMethod.Put, method);
            Xunit.Assert.Equal("/api/v1/profile/workout-preferences", relativePath);
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            ExpectedVersions.Add(document.RootElement.GetProperty("expectedVersion").GetInt32());
            PreferenceWrites++;
            var status = PreferenceWrites == 1 ? BackendApiStatus.Conflict : BackendApiStatus.Success;
            return Task.FromResult(new BackendApiResponse<TResponse>(status, default,
                ServerConcurrencyVersion: ProfileReads + 1));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
    }

    private sealed class FailedPreferenceWriteApiClient(
        BackendApiStatus writeStatus,
        string errorCode) : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            Xunit.Assert.Equal("/api/v1/profile", relativePath);
            var snapshot = new ServerProfileSnapshot(
                Name: "Synthetic",
                DateOfBirth: null,
                Height: decimal.Zero,
                Weight: decimal.Zero,
                Goal: "Strength",
                WeightGoalPace: "Steady",
                TrainingExperienceLevel: "Beginner",
                WorkoutDaysPerWeek: 0,
                AvailableWorkoutWeekdaysJson: "[]",
                CycleLength: 0,
                DietaryTags: string.Empty,
                ActivitySessionDurationsJson: "{}",
                EquipmentLevel: Equipment.FullGym.ToString(),
                PreferredWorkoutActivityTypes: string.Empty,
                UnitSystem: "Metric",
                BodyWeightUnit: "kg",
                FoodMassUnit: "g",
                HeightUnit: "cm",
                DistanceUnit: "km",
                EnergyUnit: "kcal",
                NutritionGoalsJson: "{}",
                Version: InitialProfileVersion);
            return Task.FromResult(new BackendApiResponse<TResponse>(
                BackendApiStatus.Success, (TResponse)(object)snapshot));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Xunit.Assert.Equal(HttpMethod.Put, method);
            Xunit.Assert.Equal("/api/v1/profile/workout-preferences", relativePath);
            return Task.FromResult(new BackendApiResponse<TResponse>(writeStatus, default, errorCode));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
    }

    private static WorkoutPreferencesDto CreateAllSelectedWorkoutPreferences()
    {
        var activities = Enum.GetValues<WorkoutActivityType>()
            .Where(activity => activity != WorkoutActivityType.Other)
            .Select(activity => (int)activity)
            .ToArray();
        var weekdays = Enum.GetValues<DayOfWeek>().Select(day => (int)day).ToArray();
        var durations = Enum.GetValues<WorkoutActivityType>()
            .Where(WorkoutPreferencesRule.RequiresSessionDuration)
            .ToDictionary(activity => (int)activity,
                activity => WorkoutPreferencesRule.DefaultSessionDurationMinutes(activity)!.Value);
        var frequencies = Enum.GetValues<WorkoutActivityType>()
            .Where(WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency)
            .ToDictionary(activity => (int)activity,
                activity => ActivityPreferenceDefaults.SessionsPerWeek(activity, weekdays.Length)!.Value);
        return new WorkoutPreferencesDto(activities, weekdays, durations, Equipment.FullGym.ToString(), frequencies,
            new HashSet<int>());
    }
}
