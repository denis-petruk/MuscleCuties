using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.Core.Tests;

public sealed class ProfileWorkoutPreferencesRegenerationTests
{
    private const string UnknownNumericEquipmentChoice = "999";
    private const int LegacyOtherDurationMinutes = 43;
    private const int YogaSessionsPerWeek = 1;
    private const int LegacyOtherSessionsPerWeek = 3;
    private const int SavedYogaSessionsPerWeek = 2;
    private const int SavedMobilitySessionsPerWeek = 3;
    private const int StaleOtherSessionsPerWeek = 5;
    private const int ExpectedOneSave = 1;

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
        Assert.Null(store.SavedPreferences);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task LoadingMissingProfileDoesNotCreatePreferences()
    {
        var store = new FakeStore(null);

        var result = await new ProfileWorkoutPreferencesUseCase(store).LoadAsync();

        Assert.Null(result);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task SavingNormalizedPreferencesRemovesLegacyOtherWithoutMutatingLoadedSnapshot()
    {
        var saved = new WorkoutPreferencesDto(
            [(int)WorkoutActivityType.Yoga, (int)WorkoutActivityType.Other],
            [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday],
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Yoga] = ActivitySessionDurationRule.GentleYogaDefaultMinutes,
                [(int)WorkoutActivityType.Other] = LegacyOtherDurationMinutes
            },
            RequestedSessionsPerWeek: new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Yoga] = YogaSessionsPerWeek,
                [(int)WorkoutActivityType.Other] = LegacyOtherSessionsPerWeek
            });
        var store = new FakeStore(saved);
        var useCase = new ProfileWorkoutPreferencesUseCase(store);

        Assert.Same(saved, await useCase.LoadAsync());
        Assert.Contains((int)WorkoutActivityType.Other, saved.ActivityTypes);

        var result = await useCase.SaveAsync(saved);

        Assert.True(result.Succeeded);
        Assert.NotSame(saved, store.SavedPreferences);
        Assert.Equal([(int)WorkoutActivityType.Yoga], store.SavedPreferences!.ActivityTypes);
        Assert.Equal(
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Yoga] = ActivitySessionDurationRule.GentleYogaDefaultMinutes
            },
            store.SavedPreferences.ActivitySessionDurationsMinutes);
        Assert.Equal(
            new Dictionary<int, int> { [(int)WorkoutActivityType.Yoga] = YogaSessionsPerWeek },
            store.SavedPreferences.RequestedSessionsPerWeek);
        Assert.Contains((int)WorkoutActivityType.Other, saved.ActivityTypes);
        Assert.Equal(ExpectedOneSave, store.SaveCount);
    }

    [Fact]
    public async Task SavingFlexibleActivityWithoutFrequencyDoesNotPersistOrUpdatePlan()
    {
        var store = new FakeStore(null);
        var preferences = new WorkoutPreferencesDto(
            [(int)WorkoutActivityType.Yoga],
            [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday],
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Yoga] = ActivitySessionDurationRule.GentleYogaDefaultMinutes
            });

        var result = await new ProfileWorkoutPreferencesUseCase(store).SaveAsync(preferences);

        Assert.Equal(WorkoutPreferencesError.InvalidPreferences, result.Error);
        Assert.Null(store.SavedPreferences);
        Assert.Equal(0, store.SaveCount);
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

    private sealed class FakeStore(WorkoutPreferencesDto? saved) : IProfileWorkoutPreferencesStore
    {
        public WorkoutPreferencesDto? SavedPreferences { get; private set; }
        public int SaveCount { get; private set; }

        public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(saved);

        public Task<WorkoutPreferencesError> SaveAsync(
            WorkoutPreferencesDto preferences, CancellationToken cancellationToken)
        {
            SavedPreferences = preferences;
            SaveCount++;
            return Task.FromResult(WorkoutPreferencesError.None);
        }
    }
}
