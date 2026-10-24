using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPlanGenerationRuleTests
{
    [Fact]
    public async Task LegacyGenerationStopsBeforeAccessingReferenceData()
    {
        var planner = new GenerateWorkoutPlanUseCase(null!);
        var now = DateTime.UtcNow;
        var request = new GenerateWorkoutPlanRequest(UserGoal.MaintainHealth,
            TrainingExperienceLevel.Beginner, WeekPlanRule.MinimumDaysPerWeek,
            SessionMinutesCap: 0, [WorkoutActivityType.HighVolumeStrength],
            Equipment.FullGym,
            CyclePhase.Follicular, AllowsCyclePhaseAdjustments: false,
            CreatedAtUtc: now, Today: DateOnly.FromDateTime(now));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            planner.ExecuteAsync(Guid.NewGuid(), request));

        Assert.Equal(WorkoutPlanGenerationRule.GenerationUnavailableMessage, failure.Message);
    }

    [Fact]
    public void LegacyGenerationRemainsDisabledUntilItsRulesAreComplete()
    {
        Assert.False(WorkoutPlanGenerationRule.CanGeneratePlans);
        Assert.False(string.IsNullOrWhiteSpace(WorkoutPlanGenerationRule.GenerationUnavailableMessage));
    }

    [Fact]
    public void ActivityPreferencesRetainDistinctIdentityWithoutAddingStrength()
    {
        var selected = new[]
        {
            WorkoutActivityType.RockClimbing,
            WorkoutActivityType.Hiit,
            WorkoutActivityType.Walking,
            WorkoutActivityType.Mobility,
            WorkoutActivityType.RestDay
        };

        var restored = WorkoutActivityPreferences.Parse(WorkoutActivityPreferences.Serialize(selected));

        Assert.Equal(selected.Order(), restored.Order());
        Assert.DoesNotContain(WorkoutActivityType.HighVolumeStrength, restored);
        Assert.Contains(WorkoutActivityType.RockClimbing, restored);
    }

    [Fact]
    public void ActivityPreferencesRoundTripEverySupportedActivityIdentity()
    {
        var selected = Enum.GetValues<WorkoutActivityType>();

        var restored = WorkoutActivityPreferences.Parse(WorkoutActivityPreferences.Serialize(selected));

        Assert.Equal(selected.Order(), restored.Order());
    }
}
