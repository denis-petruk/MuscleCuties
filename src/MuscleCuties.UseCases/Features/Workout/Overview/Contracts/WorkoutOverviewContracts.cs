namespace MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

/// <summary>A request for the workout overview as of a date.</summary>
public sealed record WorkoutOverviewRequest(DateOnly StartDate);

/// <summary>A request to move a planned day, which cascades over the following days.</summary>
public sealed record RescheduleWorkoutDayRequest(int WorkoutDayId, DateOnly TargetDate);

/// <summary>One session in the overview list.</summary>
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
    /// <summary>Whether she may skip the session without breaking the week's plan.</summary>
    public bool IsOptional { get; init; }
    /// <summary>Whether this entry is the lighter alternative to the day's planned session.</summary>
    public bool IsEasierAlternative { get; init; }
    /// <summary>Whether the session is open now, which decides the resume action.</summary>
    public bool IsInProgress { get; init; }
    /// <summary>The day's place in the week, absent for entries outside the plan.</summary>
    public int? DayOrder { get; init; }
}

/// <summary>One scheduled day in the overview.</summary>
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
    /// <summary>Whether the day may be skipped without breaking the week.</summary>
    public bool IsOptional { get; init; }
    /// <summary>Whether the day holds the lighter alternative.</summary>
    public bool IsEasierAlternative { get; init; }
    /// <summary>The other day this one is paired with, when the week schedules them together.</summary>
    public int? CompanionWorkoutDayId { get; init; }
    /// <summary>The sessions on that day, absent when the day is a rest day.</summary>
    public IReadOnlyList<WorkoutScheduleSessionItem>? Sessions { get; init; }
}

/// <summary>One session inside a scheduled day.</summary>
public sealed record WorkoutScheduleSessionItem(
    int WorkoutDayId,
    string Tag,
    string Title,
    int? DayOrder,
    bool IsCompleted,
    bool IsOptional,
    bool IsEasierAlternative);

/// <summary>A request to mark a session optional or required.</summary>
public sealed record SetWorkoutSessionOptionalRequest(int WorkoutDayId, bool IsOptional, bool IsEasierAlternative);

/// <summary>One activity's summary inside a session.</summary>
public sealed record WorkoutActivitySummaryDto(string Tag, string Title, int ExerciseCount);

/// <summary>The exercises previewed for a planned day.</summary>
public sealed record WorkoutDayExercisePreview(
    int WorkoutDayId,
    IReadOnlyList<string> Names,
    IReadOnlyList<WorkoutActivitySummaryDto> Activities);

/// <summary>The whole overview payload: the list, the schedule and the previews.</summary>
public sealed record WorkoutOverviewResponse(
    string? PlanName,
    string PhaseName,
    IReadOnlyList<WorkoutListEntry> Workouts,
    IReadOnlyList<WorkoutScheduleEntry> Schedule,
    IReadOnlyList<WorkoutDayExercisePreview> ExercisePreviews)
{
    /// <summary>The session open right now, which the screen offers to resume first.</summary>
    public WorkoutListEntry? InProgressWorkout { get; init; }
}
