using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

/// <summary>The account's planning state: experience, deload window, pending regenerations and the saved answers. One row per
/// account, and every field here can force a plan rebuild.</summary>
public sealed class WorkoutAccountPlanningStateRecord : ITenantRecord
{
    /// <summary>Primary key of the one state row per account.</summary>
    public Guid Id { get; set; }
    /// <summary>The account this single state row belongs to.</summary>
    public Guid UserId { get; set; }
    /// <summary>Her zone, which every local date in the plan is computed in.</summary>
    public string? TimeZoneId { get; set; }
    /// <summary>The tz-database version her zone was resolved against; a change can invalidate stored local dates.</summary>
    public long TimeZoneVersion { get; set; }
    /// <summary>What she chose at setup, never overwritten, so a promotion can be undone without asking her again.</summary>
    public TrainingExperienceLevel BaselineExperience { get; set; }
    /// <summary>What the planner currently uses, after any promotion.</summary>
    public TrainingExperienceLevel EffectiveExperience { get; set; }
    /// <summary>Bumped whenever the effective level changes, so a plan built on the old level can tell it is stale.</summary>
    public long ExperienceVersion { get; set; }
    /// <summary>The end of the evidence already counted towards a deload, so a week is not counted twice.</summary>
    public DateOnly? DeloadEvidenceThroughExclusive { get; set; }
    /// <summary>First day of the active deload window; null when no deload is running.</summary>
    public DateOnly? DeloadStartDate { get; set; }
    /// <summary>One past the last deload day, which is how the window is compared without an inclusive bound.</summary>
    public DateOnly? DeloadEndExclusive { get; set; }
    /// <summary>The plan whose prescriptions were already reduced for the active deload window.</summary>
    public Guid? DeloadAppliedPlanId { get; set; }
    /// <summary>Set when an earned promotion is waiting for the next plan build, so it is applied once.</summary>
    public bool PromotionPending { get; set; }
    /// <summary>Set when a safety change (injury, pain review) requires the plan to be rebuilt before the next session.</summary>
    public bool SafetyRegenerationPending { get; set; }
    /// <summary>Set while a cycle-driven readiness change still has to be folded into the plan.</summary>
    public bool CycleReadinessPending { get; set; }
    /// <summary>The day whose pain review is still outstanding, which keeps the change from being forgotten.</summary>
    public DateOnly? CycleReadinessPainReviewDate { get; set; }
    /// <summary>Bumped on every answer write, so a save based on an older read is refused rather than overwriting.</summary>
    public long AnswerVersion { get; set; }
    /// <summary>The saved answers as JSON, so a new question can be added without a schema migration.</summary>
    public string? AnswersJson { get; set; }
}

/// <summary>Persisted same-day dose overlay; the accepted plan remains the baseline.</summary>
public sealed class WorkoutCycleReadinessOccurrenceRecord : ITenantRecord
{
    /// <summary>Primary key of the occurrence.</summary>
    public Guid Id { get; set; }
    /// <summary>The owner of the day being overlaid.</summary>
    public Guid UserId { get; set; }
    /// <summary>The plan day this overlay applies to.</summary>
    public Guid WorkoutDayId { get; set; }
    /// <summary>The local day the overlay was computed for; the same day is only overlaid once.</summary>
    public DateOnly LocalDate { get; set; }
    /// <summary>Fingerprint of the inputs the overlay was computed from, so it is reused only while those inputs still match.</summary>
    public string InputFingerprint { get; set; } = string.Empty;
    /// <summary>The adjusted prescription as JSON; the default empty object means no change was applied.</summary>
    public string PrescriptionJson { get; set; } = "{}";
    /// <summary>When the overlay was last recomputed, in UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Independent of mutable workout/history rows: deletion cannot revoke an earned day.</summary>
public sealed class WorkoutCompletionDayCreditRecord : ITenantRecord
{
    /// <summary>Primary key of the credit.</summary>
    public Guid Id { get; set; }
    /// <summary>The owner whose earned day this is.</summary>
    public Guid UserId { get; set; }
    /// <summary>The local day credited, which the weekly count sums.</summary>
    public DateOnly LocalDate { get; set; }
    /// <summary>The log it came from when it was logged, null when derived another way.</summary>
    public Guid? SourceWorkoutLogId { get; set; }
    /// <summary>The session it came from; at most one of the two sources is set.</summary>
    public Guid? SourceSessionId { get; set; }
    /// <summary>When the day was accepted as trained, in UTC.</summary>
    public DateTime AcceptedAtUtc { get; set; }
    /// <summary>The zone the credited date was recorded in, so the day keeps its meaning later.</summary>
    public string TimeZoneId { get; set; } = string.Empty;
    /// <summary>The tz version that zone was resolved against.</summary>
    public long TimeZoneVersion { get; set; }
}
