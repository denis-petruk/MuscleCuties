using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

public sealed class WorkoutAccountPlanningStateRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? TimeZoneId { get; set; }
    public long TimeZoneVersion { get; set; }
    public TrainingExperienceLevel BaselineExperience { get; set; }
    public TrainingExperienceLevel EffectiveExperience { get; set; }
    public long ExperienceVersion { get; set; }
    public DateOnly? DeloadEvidenceThroughExclusive { get; set; }
    public DateOnly? DeloadStartDate { get; set; }
    public DateOnly? DeloadEndExclusive { get; set; }
    public bool PromotionPending { get; set; }
    public bool SafetyRegenerationPending { get; set; }
    public bool CycleReadinessPending { get; set; }
    public DateOnly? CycleReadinessPainReviewDate { get; set; }
    public long AnswerVersion { get; set; }
    public string? AnswersJson { get; set; }
}

/// <summary>Persisted same-day dose overlay; the accepted plan remains the baseline.</summary>
public sealed class WorkoutCycleReadinessOccurrenceRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkoutDayId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string InputFingerprint { get; set; } = string.Empty;
    public string PrescriptionJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Independent of mutable workout/history rows: deletion cannot revoke an earned day.</summary>
public sealed class WorkoutCompletionDayCreditRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public Guid? SourceWorkoutLogId { get; set; }
    public Guid? SourceSessionId { get; set; }
    public DateTime AcceptedAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public long TimeZoneVersion { get; set; }
}
