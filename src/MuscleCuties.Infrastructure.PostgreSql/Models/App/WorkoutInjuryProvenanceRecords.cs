namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

/// <summary>An observed injury episode. Legacy records without an observed start remain unlinked.</summary>
public sealed class WorkoutInjuryEpisodeRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid InjuryLogId { get; set; }
    public int SiteFlag { get; set; }
    public DateOnly StartedOn { get; set; }
    public DateOnly? ClearedOn { get; set; }
    public string? ClearanceTimeZoneId { get; set; }
    public long? ClearanceTimeZoneVersion { get; set; }
}

/// <summary>Original prescription captured when an injury suppresses an upcoming workout.</summary>
public sealed class WorkoutInjuryPrescriptionRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EpisodeId { get; set; }
    public Guid WorkoutDayId { get; set; }
    public Guid OriginalWorkoutDayExerciseId { get; set; }
    public int OriginalExerciseId { get; set; }
    public string? OriginalPlanningSlotId { get; set; }
    public Guid? OriginalOriginWorkoutDayExerciseId { get; set; }
    public int OriginalSets { get; set; }
    public int OriginalOptionalSets { get; set; }
    public int OriginalReps { get; set; }
    public int? OriginalDurationSeconds { get; set; }
    public bool OriginalIsOptional { get; set; }
    public bool OriginalIsRemovedByReadiness { get; set; }
    public Guid? ReplacementWorkoutDayExerciseId { get; set; }
    public int? ReplacementExerciseId { get; set; }
}
