namespace MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

public sealed record WorkoutOverviewRequest(DateOnly StartDate);

public sealed record RescheduleWorkoutDayRequest(int WorkoutDayId, DateOnly TargetDate);

public sealed record WorkoutListEntry(
    int WorkoutDayId,
    string Tag,
    string DayLabel,
    string Title,
    string Duration,
    string DetailsText,
    bool IsRestDay,
    bool IsCompleted,
    int ExerciseCount,
    int ActivityCount)
{
    public bool IsOptional { get; init; }
    public bool IsEasierAlternative { get; init; }
    public bool IsInProgress { get; init; }
    public int? DayOrder { get; init; }
}

public sealed record WorkoutScheduleEntry(
    DateOnly Date,
    int WeekIndex,
    string DayLabel,
    string Tag,
    string Title,
    int WorkoutDayId,
    bool IsToday,
    bool IsCompleted,
    bool IsRestDay)
{
    public bool IsOptional { get; init; }
    public bool IsEasierAlternative { get; init; }
    public int? CompanionWorkoutDayId { get; init; }
}

public sealed record SetWorkoutSessionOptionalRequest(int WorkoutDayId, bool IsOptional, bool IsEasierAlternative);

public sealed record WorkoutActivitySummaryDto(string Tag, string Title, int ExerciseCount);

public sealed record WorkoutDayExercisePreview(
    int WorkoutDayId,
    IReadOnlyList<string> Names,
    IReadOnlyList<WorkoutActivitySummaryDto> Activities);

public sealed record WorkoutOverviewResponse(
    string? PlanName,
    string PhaseName,
    IReadOnlyList<WorkoutListEntry> Workouts,
    IReadOnlyList<WorkoutScheduleEntry> Schedule,
    IReadOnlyList<WorkoutDayExercisePreview> ExercisePreviews)
{
    public WorkoutListEntry? InProgressWorkout { get; init; }
}
