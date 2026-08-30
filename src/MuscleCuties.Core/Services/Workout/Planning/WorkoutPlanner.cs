namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner : IWorkoutPlanner
{
    private const string GeneratedPlanPrefix = "Generated training";
    private const string PreviousGeneratedPlanPrefix = "Glow Plan";
    private const string LegacyGeneratedPlanPrefix = "Cycle Plan";
    private const int FallbackMinutesPerExercise = 12;

    private static readonly string[] GeneratedPlanNames =
    [
        "Low intensity recovery training",
        "Progressive strength training",
        "Peak strength training",
        "Controlled strength training",
        "Conditioning and strength training",
        "Interval conditioning training",
        "Low impact conditioning training",
        "Full body hypertrophy training",
        "Heavy full body training",
        "Controlled full body training",
        "Balanced strength and cardio training",
        "Balanced strength training",
        "Strength-based fat loss training",
        "Balanced recovery training",
        "Advanced physique strength",
        "Peak physique strength",
        "Controlled physique training",
        "Heavy strength training",
        "Focused strength training",
        "Express heavy strength training",
        "Quick strength training",
        "Strength and conditioning training",
        "Climbing and conditioning training",
        "Strength and recovery training",
        "Cardio and recovery training",
        "HIIT conditioning",
        "Cycling conditioning",
        "Running conditioning",
        "Swimming conditioning",
        "Easy cycling conditioning",
        "Easy running conditioning",
        "Easy swimming conditioning",
        "Technique climbing strength",
        "Yoga strength recovery"
    ];
}
