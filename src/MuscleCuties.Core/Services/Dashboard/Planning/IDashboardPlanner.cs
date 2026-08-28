using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Dashboard.Planning;

public interface IDashboardPlanner
{
    DashboardSupportSummary BuildSupportSummary(
        CyclePrediction prediction,
        CyclePhase phase,
        float caloriesProgress,
        float? weight,
        int workoutDaysPerWeek,
        TodaysWorkoutSummary workoutSummary,
        HealthWeeklySummary? healthSummary = null);
}
