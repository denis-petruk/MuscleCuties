namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

/// <summary>An observed injury episode. Legacy records without an observed start remain unlinked.</summary>
public sealed class WorkoutInjuryEpisodeRecord : ITenantRecord
{
    /// <summary>Primary key of the episode.</summary>
    public Guid Id { get; set; }
    /// <summary>The owner; every episode read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The report that opened, or last continued, this episode.</summary>
    public Guid InjuryLogId { get; set; }
    /// <summary>The body site as a stable flag, which is what lets one site's episodes be found without scanning the log text.</summary>
    public int SiteFlag { get; set; }
    /// <summary>The date the injury was first observed, in her local calendar.</summary>
    public DateOnly StartedOn { get; set; }
    /// <summary>The date it was cleared; null while the episode still restricts planning.</summary>
    public DateOnly? ClearedOn { get; set; }
    /// <summary>The zone the clearance date was recorded in, so the date still means the same day when re-read.</summary>
    public string? ClearanceTimeZoneId { get; set; }
    /// <summary>The tz-database version that zone id was resolved against; a later rules change can then detect a stale date.</summary>
    public long? ClearanceTimeZoneVersion { get; set; }
    /// <summary>Whether the originating report was deleted; the episode stays so past omissions keep their reason.</summary>
    public bool SourceDeleted { get; set; }
}

/// <summary>Original prescription captured when an injury suppresses an upcoming workout.</summary>
public sealed class WorkoutInjuryPrescriptionRecord : ITenantRecord
{
    /// <summary>Primary key of the captured prescription.</summary>
    public Guid Id { get; set; }
    /// <summary>The owner; filtered like every tenant row.</summary>
    public Guid UserId { get; set; }
    /// <summary>The episode that caused this change; clearing the episode restores every row it touched.</summary>
    public Guid EpisodeId { get; set; }
    /// <summary>The plan day whose prescription was changed.</summary>
    public Guid WorkoutDayId { get; set; }
    /// <summary>The exercise row that was replaced.</summary>
    public Guid OriginalWorkoutDayExerciseId { get; set; }
    /// <summary>The exercise as originally prescribed, copied rather than looked up because the row may be deleted.</summary>
    public int OriginalExerciseId { get; set; }
    /// <summary>The planning slot it filled, so a replacement lands in the same slot.</summary>
    public string? OriginalPlanningSlotId { get; set; }
    /// <summary>For a row that was itself a substitute, the row it replaced; null when it was in the plan from the start.</summary>
    public Guid? OriginalOriginWorkoutDayExerciseId { get; set; }
    /// <summary>The sets originally prescribed, which restoration puts back.</summary>
    public int OriginalSets { get; set; }
    /// <summary>The optional sets originally prescribed.</summary>
    public int OriginalOptionalSets { get; set; }
    /// <summary>The reps originally prescribed.</summary>
    public int OriginalReps { get; set; }
    /// <summary>The duration originally prescribed; set only for time-based work, null for rep-based.</summary>
    public int? OriginalDurationSeconds { get; set; }
    /// <summary>Whether the work was optional, which affects whether its loss counts as volume taken away.</summary>
    public bool OriginalIsOptional { get; set; }
    /// <summary>Whether readiness had already removed it, so clearing the injury does not resurrect it.</summary>
    public bool OriginalIsRemovedByReadiness { get; set; }
    /// <summary>The row that stands in while the episode is active; null when the slot was omitted instead.</summary>
    public Guid? ReplacementWorkoutDayExerciseId { get; set; }
    /// <summary>The exercise of the substitute, copied for the same reason as the original.</summary>
    public int? ReplacementExerciseId { get; set; }
}
