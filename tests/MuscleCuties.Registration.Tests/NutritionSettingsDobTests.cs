using System.Text.Json;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.Infrastructure.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Cycle.History;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using Xunit;

namespace MuscleCuties.Registration.Tests;

// S-02: the nutrition settings store feeds the planner, so an invented adult age here would
// produce calorie targets for an age the user never supplied.
public sealed class NutritionSettingsDobTests
{
    [Fact]
    public async Task Missing_date_of_birth_stays_missing_for_nutrition_settings()
    {
        var snapshot = Snapshot(dateOfBirth: null);
        var client = new ResponderClient(path => path.StartsWith("/api/v1/cycle/overview", StringComparison.Ordinal)
            ? Cycle()
            : snapshot);

        var loaded = await new CloudProfileNutritionSettingsStore(client).LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(default, loaded!.DateOfBirth);
    }

    [Fact]
    public async Task Provided_date_of_birth_is_passed_through()
    {
        var snapshot = Snapshot(new DateOnly(1995, 3, 2));
        var client = new ResponderClient(path => path.StartsWith("/api/v1/cycle/overview", StringComparison.Ordinal)
            ? Cycle()
            : snapshot);

        var loaded = await new CloudProfileNutritionSettingsStore(client).LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(new DateTime(1995, 3, 2), loaded!.DateOfBirth);
    }

    private static ServerProfileSnapshot Snapshot(DateOnly? dateOfBirth) => new(
        Name: "Synthetic", DateOfBirth: dateOfBirth, Height: 165m, Weight: 60m, Goal: "Strength",
        WeightGoalPace: "Steady", TrainingExperienceLevel: "Beginner", WorkoutDaysPerWeek: 2,
        AvailableWorkoutWeekdaysJson: "[1,4]", CycleLength: 28, DietaryTags: "",
        ActivitySessionDurationsJson: "{}", EquipmentLevel: "FullGym", PreferredWorkoutActivityTypes: "",
        UnitSystem: "Metric", BodyWeightUnit: "kg", FoodMassUnit: "g", HeightUnit: "cm", DistanceUnit: "km",
        EnergyUnit: "kcal", NutritionGoalsJson: "{}", Version: 1, Email: "synthetic@example.test");

    private static ServerCycleOverview Cycle()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new ServerCycleOverview(
            new CyclePredictionProjection(today, true, today.AddDays(-10), 11, 28, "Follicular", today.AddDays(18),
                18, today.AddDays(4), today, today.AddDays(5), "profile"),
            new CycleHistorySnapshot([], []),
            [],
            "cycle.phase.follicular",
            "cycle.prediction_source.profile");
    }

    private sealed class ResponderClient(Func<string, object> responder) : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default)
        {
            var raw = responder(relativePath);
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success,
                raw is TResponse typed ? typed : default));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            var raw = responder(relativePath);
            object response = new CloudProfileWriteResponse(2);
            var result = raw is TResponse typed ? typed : response is TResponse fallback ? fallback : default;
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, result));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
