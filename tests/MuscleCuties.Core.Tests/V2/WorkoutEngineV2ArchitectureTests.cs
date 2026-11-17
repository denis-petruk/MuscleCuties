using MuscleCuties.Core.Domain.Workout.Planning.V2;

namespace MuscleCuties.Core.Tests.V2;

public sealed class WorkoutEngineV2ArchitectureTests
{
    [Fact]
    public void Retired_legacy_generators_are_removed_and_v2_engine_is_canonical()
    {
        var coreAssembly = typeof(WorkoutGenerationEngineV2).Assembly;

        Assert.Null(coreAssembly.GetType("MuscleCuties.Core.Domain.Workout.Planning.WeeklyWorkoutPlanGeneratorRule"));
        Assert.Null(coreAssembly.GetType("MuscleCuties.Core.Domain.Workout.Planning.ExercisePickerRule"));
        Assert.Null(coreAssembly.GetType("MuscleCuties.Core.Domain.Workout.Planning.WeekPlanRule"));
        Assert.Null(coreAssembly.GetType("MuscleCuties.Core.Domain.Workout.Planning.WeekScheduleRule"));
        Assert.NotNull(coreAssembly.GetType("MuscleCuties.Core.Domain.Workout.Planning.V2.WorkoutGenerationEngineV2"));
    }
}
