namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

// A slot can have no exercise row while an injury prevents a safe replacement.
public sealed class WorkoutInjuryOmittedSlotRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public int SessionIndex { get; set; }
    public string SlotId { get; set; } = string.Empty;
    public Guid EpisodeId { get; set; }
    public Guid OriginWorkoutDayExerciseId { get; set; }
    public int OriginalExerciseId { get; set; }
    public int OriginalSets { get; set; }
    public int OriginalOptionalSets { get; set; }
    public int OriginalReps { get; set; }
    public int? OriginalDurationSeconds { get; set; }
    public bool OriginalIsOptional { get; set; }
    public bool OriginalIsRemovedByReadiness { get; set; }
}
