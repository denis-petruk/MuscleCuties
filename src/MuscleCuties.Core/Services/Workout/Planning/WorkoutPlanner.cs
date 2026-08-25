namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner : IWorkoutPlanner
{
    private const string GeneratedPlanPrefix = "Glow Plan";
    private const string LegacyGeneratedPlanPrefix = "Cycle Plan";
    private const int FallbackMinutesPerExercise = 8;
}
