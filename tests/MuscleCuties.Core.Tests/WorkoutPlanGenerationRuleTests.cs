using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;
namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPlanGenerationRuleTests
{
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
