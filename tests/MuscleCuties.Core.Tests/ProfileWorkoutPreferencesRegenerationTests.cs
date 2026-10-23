using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

namespace MuscleCuties.Core.Tests;

public sealed class ProfileWorkoutPreferencesRegenerationTests
{
    private const string UnknownNumericEquipmentChoice = "999";

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
    public async Task RegenerationUsesSavedPreferencesToReplacePlan()
    {
        var saved = new WorkoutPreferencesDto(
            [(int)WorkoutActivityType.StrengthHighIntensity],
            (int)StrengthTrainingStyle.ComfortableModerate,
            WorkoutPreferencesRule.MinimumDaysPerWeek,
            Equipment.Bodyweight.ToString());
        var store = new FakeStore(saved);

        var result = await new ProfileWorkoutPreferencesUseCase(store).RegenerateSavedPlanAsync();

        Assert.True(result.Succeeded);
        Assert.Same(saved, store.SavedPreferences);
        Assert.Equal(Equipment.Bodyweight.ToString(), store.SavedPreferences?.EquipmentLevel);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task RegenerationDoesNotCreatePlanWithoutSavedProfile()
    {
        var store = new FakeStore(null);

        var result = await new ProfileWorkoutPreferencesUseCase(store).RegenerateSavedPlanAsync();

        Assert.Equal(WorkoutPreferencesError.ProfileMissing, result.Error);
        Assert.Equal(0, store.SaveCount);
    }

    private sealed class FakeStore(WorkoutPreferencesDto? saved) : IProfileWorkoutPreferencesStore
    {
        public WorkoutPreferencesDto? SavedPreferences { get; private set; }
        public int SaveCount { get; private set; }

        public Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(saved);

        public Task<WorkoutPreferencesError> SaveAndUpdatePlanAsync(
            WorkoutPreferencesDto preferences, CancellationToken cancellationToken)
        {
            SavedPreferences = preferences;
            SaveCount++;
            return Task.FromResult(WorkoutPreferencesError.None);
        }
    }
}
