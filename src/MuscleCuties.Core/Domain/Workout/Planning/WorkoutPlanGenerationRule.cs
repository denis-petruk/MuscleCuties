namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Authority flag for plan generation availability.</summary>
public static class WorkoutPlanGenerationRule
{
    public static bool CanGeneratePlans => false;
    public const string GenerationUnavailableMessage =
        "Workout plan generation is unavailable because activity-specific prescription rules are not fully configured.";
}
