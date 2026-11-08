using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class ProfileWorkoutPreferencesRegenerationTests
{
    private const string UnknownNumericEquipmentChoice = "999";
    private const int SavedYogaSessionsPerWeek = 2;
    private const int SavedMobilitySessionsPerWeek = 3;
    private const int StaleOtherSessionsPerWeek = 5;
    private const int SyntheticProposalYear = 2026;
    private const int SyntheticProposalMonth = 11;
    private const int SyntheticProposalDay = 1;

    [Theory]
    [InlineData(null, true)]
    [InlineData("FullGym", true)]
    [InlineData("HomeDumbbellsBands", true)]
    [InlineData("Bodyweight", true)]
    [InlineData("Bodyweighty", false)]
    [InlineData(UnknownNumericEquipmentChoice, false)]
    public void EquipmentSelectionOnlyAcceptsKnownQuizChoices(string? equipmentLevel, bool expected) =>
        Assert.Equal(expected, WorkoutPreferencesRule.IsValidEquipmentLevel(equipmentLevel));

    [Fact]
    public void ProfileUpdateCannotChangeAppliedWorkoutEquipment()
    {
        var applied = Equipment.FullGym.ToString();
        Assert.True(WorkoutPreferencesRule.PreservesAppliedEquipment(null, applied));
        Assert.True(WorkoutPreferencesRule.PreservesAppliedEquipment(applied.ToLowerInvariant(), applied));
        Assert.False(WorkoutPreferencesRule.PreservesAppliedEquipment(Equipment.Bodyweight.ToString(), applied));
    }

    [Fact]
    public async Task LoadingPreferencesDoesNotSaveOrReplacePlan()
    {
        var saved = new WorkoutPreferencesDto(
            [(int)WorkoutActivityType.StrengthHighIntensity],
            [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday],
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.StrengthHighIntensity] =
                    WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            },
            Equipment.Bodyweight.ToString());
        var store = new FakeStore(saved);

        var result = await new ProfileWorkoutPreferencesUseCase(store).LoadAsync();

        Assert.Same(saved, result);
    }

    [Fact]
    public async Task LoadingMissingProfileDoesNotCreatePreferences()
    {
        var store = new FakeStore(null);

        var result = await new ProfileWorkoutPreferencesUseCase(store).LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public void CloudPreferenceSnapshotLoadsSavedFrequenciesWithoutInventingMissingValues()
    {
        var requestedFrequenciesJson =
            $"{{\"Yoga\":{SavedYogaSessionsPerWeek},\"Mobility\":{SavedMobilitySessionsPerWeek},\"Other\":{StaleOtherSessionsPerWeek}}}";
        var snapshot = ProfileSnapshot(
            requestedFrequenciesJson,
            "Yoga,Mobility,Other",
            "{\"Yoga\":15,\"Other\":43}");

        var preferences = CloudWorkoutPreferencesSnapshotMapper.ToPreferences(snapshot);

        Assert.Equal(SavedYogaSessionsPerWeek,
            preferences.RequestedSessionsPerWeek![(int)WorkoutActivityType.Yoga]);
        Assert.Equal(SavedMobilitySessionsPerWeek,
            preferences.RequestedSessionsPerWeek[(int)WorkoutActivityType.Mobility]);
        Assert.DoesNotContain((int)WorkoutActivityType.Other,
            preferences.RequestedSessionsPerWeek.Keys);
        Assert.DoesNotContain((int)WorkoutActivityType.Elliptical,
            preferences.RequestedSessionsPerWeek.Keys);

        var noFrequencySnapshot = ProfileSnapshot("{}", "Yoga", "{\"Yoga\":15}");
        var noFrequencyPreferences = CloudWorkoutPreferencesSnapshotMapper.ToPreferences(noFrequencySnapshot);

        Assert.Empty(noFrequencyPreferences.RequestedSessionsPerWeek!);
    }

    [Fact]
    public void CloudPreferenceSnapshotDoesNotInventMissingWeekdays()
    {
        var snapshot = ProfileSnapshot("{}", "GeneralStrength", "{}") with
        {
            AvailableWorkoutWeekdaysJson = "[]"
        };

        var preferences = CloudWorkoutPreferencesSnapshotMapper.ToPreferences(snapshot);

        Assert.Empty(preferences.AvailableWeekdays);
    }

    [Fact]
    public void CloudPreferenceSnapshotLoadsOnlyPositiveExerciseExclusionIds()
    {
        var snapshot = ProfileSnapshot("{}", "GeneralStrength", "{}",
            excludedExerciseIdsJson: "[101,0,-3,101,\"102\"]");

        var preferences = CloudWorkoutPreferencesSnapshotMapper.ToPreferences(snapshot);

        Assert.True(preferences.ExcludedExerciseIds!.SetEquals([101]));
    }

    private static ServerProfileSnapshot ProfileSnapshot(
        string requestedFrequenciesJson,
        string selectedActivities,
        string activityDurationsJson,
        string excludedExerciseIdsJson = "[]") => new(
        "Synthetic profile", null, 0m, 0m, "MaintainHealth", "Steady", "Unknown", 3,
        "[1,3]", 28, string.Empty, activityDurationsJson, "Bodyweight", selectedActivities,
        "Metric", "kg", "g", "cm", "km", "kcal", "{}", 1,
        RequestedActivitySessionsPerWeekJson: requestedFrequenciesJson,
        ExcludedStrengthExerciseIdsJson: excludedExerciseIdsJson);

    [Fact]
    public async Task WorkoutPreferencesTravelAsDraftPreferencesOnTheAcceptedProposalRoute()
    {
        var apiClient = new CapturingProposalApiClient();
        var store = new CloudStrengthPlanProposalStoreV020(apiClient);
        var preferences = new WorkoutPreferencesDto(
            [(int)WorkoutActivityType.GeneralStrength],
            [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday],
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.GeneralStrength] =
                    WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            },
            Equipment.FullGym.ToString());
        var request = new StrengthPlanPreviewRequestV020(
            WorkoutActivityDto.GeneralStrength,
            new DateOnly(SyntheticProposalYear, SyntheticProposalMonth, SyntheticProposalDay),
            null) { DraftPreferences = preferences };

        var failure = await Assert.ThrowsAsync<PlanningRequestFailedException>(() =>
            store.CreateAsync(request, CancellationToken.None));

        Assert.Equal(BackendApiStatus.ServerError, failure.Status);

        Assert.Equal(new[] { ("POST", "/api/v1/workouts/plan/strength-proposals") }, apiClient.Calls);
        Assert.Same(preferences, apiClient.CapturedDraftPreferences);
    }

    private sealed class CapturingProposalApiClient : IMuscleCutiesBackendApiClient
    {
        public List<(string Method, string Path)> Calls { get; } = [];
        public WorkoutPreferencesDto? CapturedDraftPreferences { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.ServerError, default));

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((method.Method, relativePath));
            if (payload is StrengthPlanPreviewRequestV020 request)
                CapturedDraftPreferences = request.DraftPreferences;
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.ServerError, default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.ServerError, false));
    }

    private sealed class FakeStore(WorkoutPreferencesDto? saved) : IProfileWorkoutPreferencesStore
    {
        public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(saved);

        public Task<bool> SaveAsync(WorkoutPreferencesDto preferences, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
