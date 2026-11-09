using System.Text.Json;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using Xunit;

namespace MuscleCuties.Registration.Tests;

// S-02: a missing date of birth, experience or cycle length must stay missing. Inventing an
// adult age would satisfy the 18-64 population gate without the user ever answering it.
public sealed class PersonalInfoNoFabricationTests
{
    [Fact]
    public async Task Missing_profile_values_load_as_unset_not_invented()
    {
        var client = new RecordingClient(Snapshot(dateOfBirth: null, experience: "", cycleLength: 0));
        var loaded = await new CloudProfilePersonalInfoStore(client).LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded.Profile);
        Assert.Equal(default, loaded.Profile!.DateOfBirth);
        Assert.Equal((int)TrainingExperienceLevel.Unknown, loaded.Profile.TrainingExperienceLevel);
        Assert.Equal(0, loaded.Profile.CycleLength);
    }

    [Fact]
    public async Task Save_never_writes_a_placeholder_birth_date_experience_or_cycle_length()
    {
        var client = new RecordingClient(Snapshot(dateOfBirth: null, experience: "Beginner", cycleLength: 30));
        var saved = await new CloudProfilePersonalInfoStore(client).SaveWithSnapshotAsync(
            new SavePersonalInfoRequest("synthetic@example.test", "Synthetic", default, 165f, 60f,
                (int)UserGoal.Strength, (int)WeightGoalPace.Steady, (int)TrainingExperienceLevel.Unknown, 0, 0, 0),
            CancellationToken.None);

        Assert.True(saved);
        var body = JsonSerializer.SerializeToElement(client.LastPayload);
        Assert.False(body.GetProperty("setDateOfBirth").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("dateOfBirth").ValueKind);
        Assert.Equal("Beginner", body.GetProperty("trainingExperienceLevel").GetString());
        Assert.Equal(30, body.GetProperty("cycleLength").GetInt32());
    }

    [Fact]
    public async Task Save_writes_a_birth_date_the_user_provided()
    {
        var client = new RecordingClient(Snapshot(dateOfBirth: null, experience: "Beginner", cycleLength: 28));
        await new CloudProfilePersonalInfoStore(client).SaveWithSnapshotAsync(
            new SavePersonalInfoRequest("synthetic@example.test", "Synthetic", new DateTime(1995, 3, 2), 165f, 60f,
                (int)UserGoal.Strength, (int)WeightGoalPace.Steady, (int)TrainingExperienceLevel.Beginner, 28, 0, 0),
            CancellationToken.None);

        var body = JsonSerializer.SerializeToElement(client.LastPayload);
        Assert.True(body.GetProperty("setDateOfBirth").GetBoolean());
        Assert.Equal("1995-03-02", body.GetProperty("dateOfBirth").GetString());
    }

    private static ServerProfileSnapshot Snapshot(DateOnly? dateOfBirth, string experience, int cycleLength) => new(
        Name: "Synthetic", DateOfBirth: dateOfBirth, Height: 165m, Weight: 60m, Goal: "Strength",
        WeightGoalPace: "Steady", TrainingExperienceLevel: experience, WorkoutDaysPerWeek: 2,
        AvailableWorkoutWeekdaysJson: "[1,4]", CycleLength: cycleLength, DietaryTags: "",
        ActivitySessionDurationsJson: "{}", EquipmentLevel: "FullGym", PreferredWorkoutActivityTypes: "",
        UnitSystem: "Metric", BodyWeightUnit: "kg", FoodMassUnit: "g", HeightUnit: "cm", DistanceUnit: "km",
        EnergyUnit: "kcal", NutritionGoalsJson: "{}", Version: 1, Email: "synthetic@example.test");

    private sealed class RecordingClient(ServerProfileSnapshot snapshot) : IMuscleCutiesBackendApiClient
    {
        public object? LastPayload { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success,
                snapshot is TResponse typed ? typed : default));

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            LastPayload = payload;
            object response = new CloudProfileWriteResponse(2);
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success,
                response is TResponse typed ? typed : default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
