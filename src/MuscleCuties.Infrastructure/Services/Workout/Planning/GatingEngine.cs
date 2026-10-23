using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IGatingEngine
{
    GatingResult Resolve(
        WorkoutType slot,
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        int consecutiveLow);
}

public sealed class GatingEngine(WorkoutPlanningConfig config) : IGatingEngine
{
    public GatingResult Resolve(
        WorkoutType slot,
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        int consecutiveLow)
    {
        return WorkoutGatingRule.Resolve(slot, profile, inputs, readiness, phase,
            consecutiveLow, config);
    }
}
