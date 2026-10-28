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
    public long AnswerVersion { get; set; }
    public string? AnswersJson { get; set; }
    public Guid? StyleOfferRevisionId { get; set; }
    public string? StyleOfferJson { get; set; }
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
