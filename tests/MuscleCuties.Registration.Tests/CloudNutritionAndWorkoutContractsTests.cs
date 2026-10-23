using System.Text.Json;
using MuscleCuties.Core.Models.Enums.Cycle;
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
        var existingCheckInId = Guid.NewGuid();
        var injuryGuid = Guid.NewGuid();
        var cycleGuid = Guid.NewGuid();

        var apiClient = new RecordingApiClient(path =>
        {
            if (path.StartsWith("/api/v1/check-ins", StringComparison.Ordinal))
            {
                var record = new DailyCheckInRecord(
                    existingCheckInId, today, 8.0m, 7.8m, 9500, 9000, 4, 1, false, 62.5m, null, null, null, 91, "High", "Ovulatory");
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

        var checkInStore = new CloudDailyCheckInStore(apiClient);
        var status = await checkInStore.GetStatusAsync(new DailyCheckInStatusRequest(today), CancellationToken.None);
        Xunit.Assert.True(status.IsCompleted);
        Xunit.Assert.Equal("91", status.ScoreText);
        Xunit.Assert.Equal("High", status.TierText);

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

    [Xunit.Fact]
    public async Task CloudProfileStoresUseVersionedServerContractsAndRoutes()
    {
        var apiClient = new RecordingApiClient(path =>
        {
            if (path == "/api/v1/profile")
            {
                return new ServerProfileSnapshot(
                    "Elena",
                    new DateOnly(1998, 5, 14),
                    168m,
                    61.5m,
                    "Strength",
                    "Steady",
                    "Advanced",
                    "ManualPhaseLogging",
                    4,
                    29,
                    "Vegetarian",
                    60,
                    "FullGym",
                    "HighVolumeStrength,Yoga,StrengthStyle:HeavyLowRep",
                    "Metric",
                    "kg",
                    "g",
                    "cm",
                    "km",
                    "kcal",
                    "{}",
                    3,
                    "elena@example.test",
                    "{\"Pregnancy\":1,\"Contraception\":2}");
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
                (int)CycleTrackingMode.ManualPhaseLogging, 4, 29, 1, 2),
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
        Xunit.Assert.Equal(4, loadedWorkoutPrefs!.DaysPerWeek);
        var savedWorkoutPrefs = await workoutPrefsStore.SaveAndUpdatePlanAsync(loadedWorkoutPrefs, CancellationToken.None);
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
}
