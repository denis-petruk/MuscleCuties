using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

public sealed record GenerateWorkoutPlanRequest(
    UserGoal Goal,
    TrainingExperienceLevel Experience,
    int DaysPerWeek,
    int SessionMinutesCap,
    IReadOnlyList<WorkoutActivityType> SelectedActivities,
    StrengthTrainingStyle Style,
    Equipment Equipment,
    CyclePhase Phase,
    bool AllowsCyclePhaseAdjustments,
    DateTime CreatedAtUtc,
    DateOnly Today,
    IReadOnlyList<MuscleCuties.Core.Models.Workout.Planning.Injury>? Injuries = null,
    int BaselineSteps = 8000,
    CyclePhaseBaselines? Baselines = null,
    DailyInputs? TodayInputs = null,
    int ConsecutiveLowDays = 0);

public sealed record GenerateWorkoutPlanResponse(
    string Name,
    CyclePhase CyclePhaseTarget,
    DateTime CreatedAtUtc,
    IReadOnlyList<GeneratedWorkoutDayResponse> WorkoutDays);

public sealed record GeneratedWorkoutDayResponse(
    int DayOfWeek,
    int WeekIndex,
    DateTime Date,
    WorkoutType WorkoutType,
    string Name,
    IReadOnlyList<GeneratedWorkoutDayExerciseResponse> Exercises);

public sealed record GeneratedWorkoutDayExerciseResponse(
    int ExerciseId,
    string ExerciseCode,
    string ExerciseName,
    int Sets,
    int Reps,
    int? DurationSeconds);
