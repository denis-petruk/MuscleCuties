namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

// A slot can have no exercise row while an injury prevents a safe replacement.
/// <summary>A slot withheld from a plan because of an injury, holding what it would have prescribed so it can be restored exactly.</summary>
public sealed class WorkoutInjuryOmittedSlotRecord : ITenantRecord
{
    /// <summary>Primary key of the omission.</summary>
    public Guid Id { get; set; }
    /// <summary>The account the plan belongs to.</summary>
    public Guid UserId { get; set; }
    /// <summary>The plan the withheld slot belongs to.</summary>
    public Guid PlanId { get; set; }
    /// <summary>The day the slot would have been trained, in her local calendar.</summary>
    public DateOnly ScheduledDate { get; set; }
    /// <summary>The template the slot came from, so the slot can be matched after a regeneration.</summary>
    public string TemplateId { get; set; } = string.Empty;
    /// <summary>The template version used, so a template change is detectable.</summary>
    public string TemplateVersion { get; set; } = string.Empty;
    /// <summary>Which session of the day the slot belonged to.</summary>
    public int SessionIndex { get; set; }
    /// <summary>The planning slot that was withheld.</summary>
    public string SlotId { get; set; } = string.Empty;
    /// <summary>The injury episode that caused the omission.</summary>
    public Guid EpisodeId { get; set; }
    /// <summary>The exercise row the slot would have prescribed, kept so the omission can be reversed.</summary>
    public Guid OriginWorkoutDayExerciseId { get; set; }
    /// <summary>The exercise as planned, copied because the row may be deleted.</summary>
    public int OriginalExerciseId { get; set; }
    /// <summary>Sets that were withheld.</summary>
    public int OriginalSets { get; set; }
    /// <summary>Optional sets that were withheld.</summary>
    public int OriginalOptionalSets { get; set; }
    /// <summary>Reps that were withheld.</summary>
    public int OriginalReps { get; set; }
    /// <summary>Duration withheld, set only for time-based work.</summary>
    public int? OriginalDurationSeconds { get; set; }
    /// <summary>Whether the work was optional, so restoring it does not overstate the volume lost.</summary>
    public bool OriginalIsOptional { get; set; }
    /// <summary>Whether readiness had already removed it, so clearing the injury does not resurrect it.</summary>
    public bool OriginalIsRemovedByReadiness { get; set; }
}
